using Company.Product.Application.Common;
using Company.Product.Domain.Orders;
using Company.Product.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Company.Product.Infrastructure.Tests.Persistence;

public sealed class OrderReadServiceTests
{
    [Fact]
    public async Task GetOrdersAsync_ReturnsPagedProjectedItems()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orders-{Guid.NewGuid():N}")
            .Options;

        await using (var setupContext = new AppDbContext(options))
        {
            setupContext.Orders.Add(CreateOrder("SKU-1", "Mouse", 30m));
            setupContext.Orders.Add(CreateOrder("SKU-2", "Keyboard", 70m));
            await setupContext.SaveChangesAsync();
        }

        await using var readContext = new AppDbContext(options);
        var service = new OrderReadService(readContext);

        var result = await service.GetOrdersAsync(new OrderListQuery(null, 1, 1), CancellationToken.None);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Items[0].TotalAmount.Should().BeGreaterThan(0);
    }

    private static Order CreateOrder(string sku, string name, decimal unitPrice)
    {
        return Order.Create(
            Guid.NewGuid(),
            DateTime.UtcNow,
            [new OrderItem(Guid.NewGuid(), sku, name, unitPrice, 1)]);
    }
}
