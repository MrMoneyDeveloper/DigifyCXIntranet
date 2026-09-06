using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Company.Product.Api.Authorization;
using Company.Product.Api.IntegrationTests.Support;
using Company.Product.Api.Options;
using Company.Product.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Company.Product.Api.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public ApiSmokeTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ping_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/operational/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrdersEndpoint_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/v1/orders?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiErrorCodes.Unauthorized, document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Ping_ReturnsSecurityHeaders()
    {
        var response = await _client.GetAsync("/api/v1/operational/ping");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(
            "default-src 'none'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Ping_PreservesValidCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/operational/ping");
        request.Headers.Add("X-Correlation-ID", "integration-request-123");

        var response = await _client.SendAsync(request);

        Assert.Equal("integration-request-123", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Ping_ReplacesUnsafeCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/operational/ping");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "unsafe/value");

        var response = await _client.SendAsync(request);
        var correlationId = response.Headers.GetValues("X-Correlation-ID").Single();

        Assert.NotEqual("unsafe/value", correlationId);
        Assert.DoesNotContain('/', correlationId);
    }

    [Fact]
    public async Task CorsPreflight_AllowsConfiguredOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/orders");
        request.Headers.Add("Origin", "https://intranet.company.local");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization, Content-Type, X-Correlation-ID");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://intranet.company.local", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task CreateOrder_InvalidPayload_ReturnsValidationProblemDetails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.Add(TestAuthHandler.HeaderName, "true");
        request.Content = JsonContent.Create(new
        {
            customerId = Guid.Empty,
            items = Array.Empty<object>()
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        Assert.Equal(ApiErrorCodes.ValidationFailed, document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CreateOrder_ReadOnlyScope_ReturnsForbiddenProblemDetails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.Add(TestAuthHandler.HeaderName, ApiPolicies.OrdersRead);
        request.Content = JsonContent.Create(new
        {
            customerId = Guid.NewGuid(),
            items = new[]
            {
                new { sku = "SKU-1", name = "Item", unitPrice = 10m, quantity = 1 }
            }
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiErrorCodes.Forbidden, document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CreateOrder_UnknownProperty_IsRejectedByStrictSchema()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.Add(TestAuthHandler.HeaderName, "true");
        request.Content = JsonContent.Create(new
        {
            customerId = Guid.NewGuid(),
            items = new[]
            {
                new { sku = "SKU-1", name = "Item", unitPrice = 10m, quantity = 1 }
            },
            isAdministrator = true
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiErrorCodes.ValidationFailed, document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CreateOrder_OversizedBody_IsRejectedBeforeModelBinding()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.Add(TestAuthHandler.HeaderName, "true");
        request.Content = new ByteArrayContent(new byte[new ApiRequestLimitsOptions().MaxRequestBodyBytes + 1]);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiErrorCodes.RequestTooLarge, document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task OpenApi_UsesStrictRequestSchemas()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("CreateOrderRequest");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());

        var paths = document.RootElement.GetProperty("paths");
        Assert.False(paths.GetProperty("/api/v1/operational/ping").GetProperty("get").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/v1/orders").GetProperty("post").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task Authorization_DefaultsToAuthenticatedUsers()
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }
}
