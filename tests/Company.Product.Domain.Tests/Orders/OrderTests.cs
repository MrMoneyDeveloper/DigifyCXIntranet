using Company.Product.Domain.Common;
using Company.Product.Domain.Orders;
using FluentAssertions;

namespace Company.Product.Domain.Tests.Orders;

public sealed class OrderTests
{
    [Fact]
    public void Order_CannotTransitionToPaid_WhenCancelled()
    {
        var order = CreateValidOrder();
        order.Cancel();

        var action = () => order.MarkPaid();

        action.Should().Throw<DomainException>()
            .Which.Code.Should().Be("ORDER_INVALID_STATE");
    }

    [Fact]
    public void Order_CreatesRoundedTotalAmount()
    {
        var order = CreateValidOrder();

        order.TotalAmount.Should().Be(20.16m);
    }

    private static Order CreateValidOrder()
    {
        var items = new[]
        {
            new OrderItem(Guid.NewGuid(), "SKU-1", "Notebook", 10.10m, 1),
            new OrderItem(Guid.NewGuid(), "SKU-2", "Pen", 5.025m, 2)
        };

        return Order.Create(Guid.NewGuid(), DateTime.UtcNow, items);
    }
}
