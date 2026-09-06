namespace DigifyCXIntranet.Services;

public sealed class AccessDeniedAuditMiddleware
{
    private readonly RequestDelegate _next;

    public AccessDeniedAuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        await _next(context);

        if (!IsAccessDeniedResponse(
                context.Response.StatusCode,
                context.Response.Headers.Location.ToString()))
        {
            return;
        }

        await auditService.WriteAsync(
            UserNameHelper.GetShortName(context.User),
            "AccessDenied",
            "Authorization",
            "policy-or-role-requirement-not-met",
            succeeded: false,
            errorCode: "Forbidden",
            httpContext: context,
            cancellationToken: context.RequestAborted);
    }

    internal static bool IsAccessDeniedResponse(int statusCode, string? location)
    {
        if (statusCode == StatusCodes.Status403Forbidden)
        {
            return true;
        }

        if (statusCode is not (StatusCodes.Status301MovedPermanently or
            StatusCodes.Status302Found or
            StatusCodes.Status303SeeOther or
            StatusCodes.Status307TemporaryRedirect or
            StatusCodes.Status308PermanentRedirect) ||
            string.IsNullOrWhiteSpace(location))
        {
            return false;
        }

        var path = Uri.TryCreate(location, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : location.Split('?', 2)[0];

        return string.Equals(path, "/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);
    }
}
