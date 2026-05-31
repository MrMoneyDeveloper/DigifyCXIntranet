using Company.Product.Application.Common;
using Company.Product.Contracts.Errors;
using Company.Product.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Company.Product.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        var (statusCode, title, errorCode, errors) = exception switch
        {
            ApplicationValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                ApiErrorCodes.ValidationFailed,
                validation.Errors),
            ResourceNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                ApiErrorCodes.ResourceNotFound,
                (IReadOnlyDictionary<string, string[]>?)null),
            DomainException domain => (
                StatusCodes.Status409Conflict,
                "Domain rule violation",
                domain.Code,
                (IReadOnlyDictionary<string, string[]>?)null),
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                ApiErrorCodes.Forbidden,
                (IReadOnlyDictionary<string, string[]>?)null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected server error",
                ApiErrorCodes.Unexpected,
                (IReadOnlyDictionary<string, string[]>?)null)
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled server exception. CorrelationId: {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogWarning(exception, "Handled request exception. CorrelationId: {CorrelationId}", correlationId);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{statusCode}",
            Detail = statusCode >= 500 ? "An unexpected error occurred." : exception.Message
        };

        problem.Extensions["errorCode"] = errorCode;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }
}
