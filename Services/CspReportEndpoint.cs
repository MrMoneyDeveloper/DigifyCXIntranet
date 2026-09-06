using System.Text.Json;

namespace DigifyCXIntranet.Services;

public static class CspReportEndpoint
{
    public const int MaxReportBytes = 16 * 1024;

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength > MaxReportBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            // Read at most one byte beyond the limit, including chunked requests
            // that do not declare Content-Length. Never buffer an unbounded body.
            var buffer = new byte[MaxReportBytes + 1];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await context.Request.Body.ReadAsync(buffer.AsMemory(length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                length += read;
            }

            if (length > MaxReportBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            using var document = JsonDocument.Parse(
                buffer.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 8 });
            var report = CspViolationReport.Parse(document.RootElement);
            if (report is null)
            {
                return Results.BadRequest();
            }

            loggerFactory.CreateLogger("ContentSecurityPolicy").LogWarning(
                "CSP violation. Disposition={Disposition} Directive={Directive} EffectiveDirective={EffectiveDirective} BlockedUri={BlockedUri} DocumentUri={DocumentUri}",
                report.Disposition, report.ViolatedDirective, report.EffectiveDirective,
                report.BlockedUri, report.DocumentUri);
            return Results.NoContent();
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status413PayloadTooLarge)
        {
            // Kestrel/IIS can enforce the endpoint limit before our reader does.
            return Results.StatusCode(ex.StatusCode);
        }
    }
}
