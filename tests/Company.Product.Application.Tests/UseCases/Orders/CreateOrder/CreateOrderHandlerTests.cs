using Company.Product.Application.Abstractions.Persistence;
using Company.Product.Application.Abstractions.Services;
using Company.Product.Application.UseCases.Orders.CreateOrder;
using Company.Product.Domain.Orders;
using FluentAssertions;

namespace Company.Product.Application.Tests.UseCases.Orders.CreateOrder;

public sealed class CreateOrderHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsOrderAndReturnsIdentifier()
    {
        var repository = new InMemoryOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc));
        var handler = new CreateOrderHandler(repository, unitOfWork, clock);

        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            [new CreateOrderItemInput("LAP-15", "Laptop", 1999.99m, 1)]);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.OrderId.Should().NotBeEmpty();
        repository.StoredOrders.Should().ContainSingle();
        repository.StoredOrders[0].Id.Should().Be(result.OrderId);
        unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_RejectsEmptyCustomerId()
    {
        var handler = new CreateOrderHandler(new InMemoryOrderRepository(), new FakeUnitOfWork(), new FakeClock(DateTime.UtcNow));

        var command = new CreateOrderCommand(Guid.Empty, [new CreateOrderItemInput("SKU", "Keyboard", 100m, 1)]);

        var action = () => handler.HandleAsync(command, CancellationToken.None);

        await action.Should().ThrowAsync<Company.Product.Application.Common.ApplicationValidationException>();
    }

    private sealed class InMemoryOrderRepository : IOrderRepository
    {
        public List<Order> StoredOrders { get; } = [];

        public Task AddAsync(Order order, CancellationToken cancellationToken)
        {
            StoredOrders.Add(order);
            return Task.CompletedTask;
        }

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
        {
            return Task.FromResult(StoredOrders.SingleOrDefault(x => x.Id == orderId));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
