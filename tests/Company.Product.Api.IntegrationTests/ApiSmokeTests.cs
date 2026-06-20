using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Company.Product.Api.IntegrationTests.Support;
using Company.Product.Contracts.Errors;

namespace Company.Product.Api.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(ApiWebApplicationFactory factory)
    {
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
    }

    [Fact]
    public async Task Ping_ReturnsSecurityHeaders()
    {
        var response = await _client.GetAsync("/api/v1/operational/ping");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
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
}
