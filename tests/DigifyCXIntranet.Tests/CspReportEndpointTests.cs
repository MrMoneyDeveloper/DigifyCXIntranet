using System.Text;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigifyCXIntranet.Tests;

public class CspReportEndpointTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Policy_OnlyUpgradesResourcesForHttps(bool isHttps)
    {
        var policy = SecurityHeadersMiddleware.BuildContentSecurityPolicy("/security/csp-report", isHttps);

        policy.Contains("upgrade-insecure-requests").Should().Be(isHttps);
        policy.Should().Contain("object-src 'none'").And.Contain("frame-ancestors 'none'");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"text\"")]
    [InlineData("{\"csp-report\": []}")]
    [InlineData("{\"body\": null}")]
    [InlineData("{")]
    [InlineData("")]
    public async Task InvalidReport_ReturnsBadRequestInsteadOfServerError(string json)
    {
        var context = CreateContext(Encoding.UTF8.GetBytes(json));

        var result = await CspReportEndpoint.HandleAsync(context, NullLoggerFactory.Instance, default);

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData("{\"csp-report\": {\"effective-directive\": \"script-src\"}}")]
    [InlineData("[{\"body\": {\"effectiveDirective\": \"script-src\"}}]")]
    public async Task BrowserReportFormats_AreAccepted(string json)
    {
        var context = CreateContext(Encoding.UTF8.GetBytes(json));

        var result = await CspReportEndpoint.HandleAsync(context, NullLoggerFactory.Instance, default);

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task UnknownLengthOversizedReport_StopsReadingAtLimit()
    {
        var context = CreateContext(new byte[CspReportEndpoint.MaxReportBytes * 10]);

        var result = await CspReportEndpoint.HandleAsync(context, NullLoggerFactory.Instance, default);

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        context.Request.Body.Position.Should().Be(CspReportEndpoint.MaxReportBytes + 1);
    }

    [Fact]
    public async Task DeclaredOversizedReport_IsRejectedBeforeReading()
    {
        var context = CreateContext(new byte[1]);
        context.Request.ContentLength = CspReportEndpoint.MaxReportBytes + 1;

        var result = await CspReportEndpoint.HandleAsync(context, NullLoggerFactory.Instance, default);

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        context.Request.Body.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReportExactlyAtLimit_IsAccepted()
    {
        var json = "{}".PadRight(CspReportEndpoint.MaxReportBytes);
        var context = CreateContext(Encoding.UTF8.GetBytes(json));

        var result = await CspReportEndpoint.HandleAsync(context, NullLoggerFactory.Instance, default);

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    private static DefaultHttpContext CreateContext(byte[] body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(body);
        return context;
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    public async Task ServerBodyRejection_ReturnsOriginalClientError(int statusCode)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new RejectedBodyStream(statusCode);

        var result = await CspReportEndpoint.HandleAsync(context, NullLoggerFactory.Instance, default);

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(statusCode);
    }

    private sealed class RejectedBodyStream(int statusCode) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new BadHttpRequestException("Request body rejected.", statusCode));
    }
}
