using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly BrowserSecurityOptions _options;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<BrowserSecurityOptions> options,
        IWebHostEnvironment environment)
    {
        _next = next;
        _options = options.Value;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers["X-Frame-Options"] = "DENY";
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
            headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin-allow-popups");
            headers.TryAdd("Cross-Origin-Resource-Policy", "same-site");
            headers.TryAdd("X-DNS-Prefetch-Control", "off");

            if (RequiresNoStore(context))
            {
                headers.CacheControl = "no-store, no-cache, max-age=0";
                headers.Pragma = "no-cache";
            }

            if (_options.EnableContentSecurityPolicy &&
                (!_environment.IsDevelopment() || _options.EnableInDevelopment))
            {
                var headerName = _options.ReportOnly
                    ? "Content-Security-Policy-Report-Only"
                    : "Content-Security-Policy";
                headers.TryAdd(headerName, BuildContentSecurityPolicy(_options.ReportPath, context.Request.IsHttps));
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    internal static string BuildContentSecurityPolicy(string reportPath, bool upgradeInsecureRequests = true)
    {
        return string.Join("; ", new[]
        {
            "default-src 'self'",
            "base-uri 'none'",
            "object-src 'none'",
            "frame-ancestors 'none'",
            "form-action 'self'",
            "script-src 'self' https://static.zdassets.com https://*.zdassets.com https://*.zendesk.com https://*.zopim.com",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' data: https://fonts.gstatic.com https://*.zdassets.com",
            "img-src 'self' data: blob: https:",
            "connect-src 'self' https://*.zdassets.com https://*.zendesk.com https://*.zopim.com wss://*.zendesk.com wss://*.zopim.com",
            "frame-src https://*.zendesk.com https://*.zopim.com",
            "media-src 'self' data: blob: https://*.zdassets.com https://*.zendesk.com https://*.zopim.com",
            "worker-src 'self' blob:",
            "manifest-src 'self'",
            $"report-uri {reportPath}"
        }.Concat(upgradeInsecureRequests ? new[] { "upgrade-insecure-requests" } : Array.Empty<string>()));
    }

    internal static bool RequiresNoStore(HttpContext context)
    {
        var path = context.Request.Path;
        return context.User.Identity?.IsAuthenticated == true ||
               path.StartsWithSegments("/Account") ||
               path.StartsWithSegments("/Admin") ||
               path.StartsWithSegments("/Finance") ||
               path.StartsWithSegments("/Policies/ComplianceReport");
    }
}
