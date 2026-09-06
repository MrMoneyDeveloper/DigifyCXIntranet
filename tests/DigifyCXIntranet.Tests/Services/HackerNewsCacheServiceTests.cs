using System.Net;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Tests.Services;

public class HackerNewsCacheServiceTests
{
    [Fact]
    public async Task Refresh_KeepsValidStoriesWhenOtherItemsAreMissingMalformedOrUnavailable()
    {
        using var client = new HttpClient(new StubHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath switch
            {
                "/v0/topstories.json" => Json("[1,2,3,4,5,6,7]"),
                "/v0/item/1.json" => Json("null"),
                "/v0/item/2.json" => Json("{bad json"),
                "/v0/item/3.json" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                "/v0/item/4.json" => Json("""{"title":"Deleted","deleted":true}"""),
                "/v0/item/5.json" => Json("""{"title":"Valid","url":"https://example.com/story","by":"author","time":123}"""),
                "/v0/item/6.json" => Json("""{"title":"Dead","dead":true}"""),
                _ => Json("""{"title":"Optional fields","url":12,"by":false,"time":"invalid"}""")
            })));
        var service = CreateService(client);

        await service.RefreshAsync();

        service.GetCurrentItems().Select(x => x.Id).Should().Equal(5, 7);
        service.GetCurrentItems().First().TimeUnix.Should().Be(123);
        service.GetCurrentItems().Last().TimeUnix.Should().Be(0);
        service.LastUpdatedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_FeedFailurePropagatesAndPreservesPreviousSnapshot()
    {
        var fail = false;
        using var client = new HttpClient(new StubHandler((request, _) => Task.FromResult(
            fail ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) :
            request.RequestUri!.AbsolutePath.EndsWith("topstories.json")
                ? Json("[1]") : Json("""{"title":"Cached"}"""))));
        var service = CreateService(client);
        await service.RefreshAsync();
        var updated = service.LastUpdatedUtc;
        fail = true;

        var refresh = () => service.RefreshAsync();

        await refresh.Should().ThrowAsync<HttpRequestException>();
        service.GetCurrentItems().Should().ContainSingle().Which.Title.Should().Be("Cached");
        service.LastUpdatedUtc.Should().Be(updated);
    }

    [Fact]
    public async Task Refresh_NoUsableStoriesPropagatesAndPreservesPreviousSnapshot()
    {
        var missing = false;
        using var client = new HttpClient(new StubHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("topstories.json")
                ? Json("[1]") : Json(missing ? "null" : """{"title":"Cached"}"""))));
        var service = CreateService(client);
        await service.RefreshAsync();
        var updated = service.LastUpdatedUtc;
        missing = true;

        var refresh = () => service.RefreshAsync();

        await refresh.Should().ThrowAsync<InvalidOperationException>();
        service.GetCurrentItems().Should().ContainSingle().Which.Title.Should().Be("Cached");
        service.LastUpdatedUtc.Should().Be(updated);
    }

    [Fact]
    public async Task Refresh_CancellationPropagatesWithoutPublishingAPartialSnapshot()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new StubHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Json("[]");
        }));
        var service = CreateService(client);

        var refresh = () => service.RefreshAsync(cancellation.Token);

        await refresh.Should().ThrowAsync<OperationCanceledException>();
        service.GetCurrentItems().Should().BeEmpty();
        service.LastUpdatedUtc.Should().BeNull();
    }

    private static HackerNewsCacheService CreateService(HttpClient client) => new(
        new StubClientFactory(client),
        Microsoft.Extensions.Options.Options.Create(new TechNewsOptions()),
        NullLogger<HackerNewsCacheService>.Instance);

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class StubClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
