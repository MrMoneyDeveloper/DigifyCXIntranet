using Company.Product.Api.Options;
using Company.Product.Contracts.Errors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Company.Product.Api.Middleware;

public sealed class RequestBodyLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiRequestLimitsOptions _options;
    private readonly ILogger<RequestBodyLimitMiddleware> _logger;

    public RequestBodyLimitMiddleware(
        RequestDelegate next,
        IOptions<ApiRequestLimitsOptions> options,
        ILogger<RequestBodyLimitMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = _options.MaxRequestBodyBytes;
        }

        if (context.Request.ContentLength > _options.MaxRequestBodyBytes)
        {
            _logger.LogWarning(
                "API request body rejected. Method={Method} Path={Path} ContentLength={ContentLength} Limit={Limit} RemoteIp={RemoteIp} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Request.ContentLength,
                _options.MaxRequestBodyBytes,
                context.Connection.RemoteIpAddress,
                context.TraceIdentifier);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Request body too large",
                Detail = "The request body exceeds the permitted size."
            };
            problem.Extensions["errorCode"] = ApiErrorCodes.RequestTooLarge;
            problem.Extensions["correlationId"] = context.TraceIdentifier;

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
            return;
        }

        await _next(context);
    }
}
