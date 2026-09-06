using System.Text.Json;
using Company.Product.ContractTests.Support;

namespace Company.Product.ContractTests;

public sealed class OpenApiContractTests : IClassFixture<ContractWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(ContractWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiDocument_ContainsOrdersContract()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);

        Assert.True(document.RootElement.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/api/v1/orders", out var ordersPath));
        Assert.True(ordersPath.TryGetProperty("get", out _));
        Assert.True(ordersPath.TryGetProperty("post", out _));
    }
}
