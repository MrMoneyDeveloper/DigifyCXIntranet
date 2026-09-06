using Company.Product.Application;
using Company.Product.Infrastructure;
using Company.Product.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Company.Product.Api.Authorization;
using Company.Product.Api.Middleware;
using Company.Product.Api.OpenApi;
using Company.Product.Api.Options;
using Company.Product.Infrastructure.Persistence;
using Company.Product.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.IIS;
using System.Threading.RateLimiting;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var configuredRequestLimits = builder.Configuration
    .GetSection(ApiRequestLimitsOptions.SectionName)
    .Get<ApiRequestLimitsOptions>() ?? new ApiRequestLimitsOptions();
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = configuredRequestLimits.MaxRequestBodyBytes;
});
builder.Services.Configure<IISServerOptions>(options =>
    options.MaxRequestBodySize = configuredRequestLimits.MaxRequestBodyBytes);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var validationErrors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value." : error.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Type = "https://httpstatuses.com/400"
            };

            problem.Extensions["errorCode"] = Company.Product.Contracts.Errors.ApiErrorCodes.ValidationFailed;
            problem.Extensions["errors"] = validationErrors;
            problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOptions<ApiRequestLimitsOptions>()
    .Bind(builder.Configuration.GetSection(ApiRequestLimitsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ApiRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(ApiRateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.OrdersWritePermitLimit <= options.GlobalPermitLimit,
        "RateLimits:OrdersWritePermitLimit cannot exceed RateLimits:GlobalPermitLimit.")
    .ValidateOnStart();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
});

var allowedOrigins = builder.Configuration
    .GetSection("Security:Cors:AllowedOrigins")
    .Get<string[]>() ?? ["https://intranet.company.local"];
if (allowedOrigins.Length == 0 ||
    allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                                 (!builder.Environment.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps)))
{
    throw new InvalidOperationException("Security:Cors:AllowedOrigins must contain valid absolute HTTPS origins outside development.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Authorization", "Content-Type", CorrelationIdMiddleware.HeaderName);
    });
});

var rateLimitOptions = builder.Configuration
    .GetSection(ApiRateLimitOptions.SectionName)
    .Get<ApiRateLimitOptions>() ?? new ApiRateLimitOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");
        logger.LogWarning(
            "Rate limit rejected {Path} for {RemoteIp}. CorrelationId={CorrelationId}",
            httpContext.Request.Path,
            httpContext.Connection.RemoteIpAddress,
            httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Please wait and try again."
        };
        problem.Extensions["errorCode"] = Company.Product.Contracts.Errors.ApiErrorCodes.RateLimitExceeded;
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = Math.Max(
                    1,
                    (int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
        }
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = GetRateLimitPartitionKey(context, "global");

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.GlobalPermitLimit,
            Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("orders-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(context, "orders-write"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.OrdersWritePermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

static string GetRateLimitPartitionKey(HttpContext context, string policyName)
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? context.User.Identity.Name
            ?? "unknown";
        return $"{policyName}:user:{subject}";
    }

    return $"{policyName}:ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Company Product API",
        Version = "v1",
        Description = "Production-oriented modular monolith API blueprint."
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Bearer token authentication"
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.SchemaFilter<StrictRequestSchemaFilter>();
    options.OperationFilter<AuthorizationOperationFilter>();
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.Authority, UriKind.Absolute, out var uri) &&
                         (!options.RequireHttpsMetadata || uri.Scheme == Uri.UriSchemeHttps),
        "Authentication:Jwt:Authority must be an absolute HTTPS URL when HTTPS metadata is required.")
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        options.Authority = jwt.Authority;
        options.Audience = jwt.Audience;
        options.RequireHttpsMetadata = jwt.RequireHttpsMetadata;

        options.TokenValidationParameters.ValidAudience = jwt.Audience;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(ApiPolicies.OrdersRead, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireScope(ApiPolicies.OrdersRead);
    });

    options.AddPolicy(ApiPolicies.OrdersWrite, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireScope(ApiPolicies.OrdersWrite);
    });
});

var app = builder.Build();
var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
var configuredConnectionString = app.Configuration.GetConnectionString(databaseOptions.ConnectionStringName);
if (string.IsNullOrWhiteSpace(configuredConnectionString))
{
    throw new InvalidOperationException($"Connection string '{databaseOptions.ConnectionStringName}' was not found.");
}

SqlServerConnectionSecurity.Validate(configuredConnectionString, app.Environment.IsDevelopment(), app.Logger);

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

var runMigrations = databaseOptions.RunMigrationsOnStartup;
if (runMigrations)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestBodyLimitMiddleware>();
app.UseMiddleware<ApiAccessResultMiddleware>();

app.UseHttpsRedirection();
app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Company Product API v1"));
}
app.UseRouting();
app.UseCors("DefaultCors");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();

public partial class Program;
