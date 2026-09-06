using System.Security.Claims;
using Company.Product.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Company.Product.Api.Middleware;

public sealed class ApiAccessResultMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiAccessResultMiddleware> _logger;

    public ApiAccessResultMiddleware(
        RequestDelegate next,
        ILogger<ApiAccessResultMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.HasStarted ||
            context.Response.ContentLength is > 0 ||
            context.Response.StatusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        {
            return;
        }

        var forbidden = context.Response.StatusCode == StatusCodes.Status403Forbidden;
        var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? "anonymous";

        _logger.LogWarning(
            "API access rejected. StatusCode={StatusCode} Method={Method} Path={Path} Subject={Subject} RemoteIp={RemoteIp} CorrelationId={CorrelationId}",
            context.Response.StatusCode,
            context.Request.Method,
            context.Request.Path,
            subject,
            context.Connection.RemoteIpAddress,
            correlationId);

        var problem = new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = forbidden ? "Forbidden" : "Unauthorized",
            Detail = forbidden
                ? "The authenticated identity does not have permission for this operation."
                : "Authentication is required for this operation."
        };
        problem.Extensions["errorCode"] = forbidden ? ApiErrorCodes.Forbidden : ApiErrorCodes.Unauthorized;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }
}
