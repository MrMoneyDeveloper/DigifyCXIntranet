using Company.Product.Application.Abstractions.Persistence;
using Company.Product.Application.Abstractions.Services;
using Company.Product.Application.Common;
using Company.Product.Domain.Orders;

namespace Company.Product.Application.UseCases.Orders.CreateOrder;

public interface ICreateOrderHandler
{
    Task<CreateOrderResult> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken);
}

public sealed class CreateOrderHandler : ICreateOrderHandler
{
    private const int MaxItems = 100;

    private readonly IClock _clock;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<CreateOrderResult> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }

        var items = command.Items
            .Select(item => new OrderItem(Guid.NewGuid(), item.Sku, item.Name, item.UnitPrice, item.Quantity))
            .ToList();

        var order = Order.Create(command.CustomerId, _clock.UtcNow, items);
        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id);
    }

    private static Dictionary<string, string[]> Validate(CreateOrderCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        if (command.CustomerId == Guid.Empty)
        {
            errors[nameof(command.CustomerId)] = ["CustomerId is required."];
        }

        if (command.Items.Count == 0)
        {
            errors[nameof(command.Items)] = ["At least one order item is required."];
            return errors;
        }

        if (command.Items.Count > MaxItems)
        {
            errors[nameof(command.Items)] = [$"A maximum of {MaxItems} items is allowed per order."];
        }

        var itemErrors = command.Items
            .Select((item, index) => new { index, item })
            .Where(entry => string.IsNullOrWhiteSpace(entry.item.Sku) || string.IsNullOrWhiteSpace(entry.item.Name) || entry.item.UnitPrice <= 0 || entry.item.Quantity <= 0)
            .Select(entry => $"Items[{entry.index}] is invalid.")
            .ToArray();

        if (itemErrors.Length > 0)
        {
            errors[nameof(command.Items)] = itemErrors;
        }

        return errors;
    }
}
