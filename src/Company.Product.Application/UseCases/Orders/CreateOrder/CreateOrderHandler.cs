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
    private readonly IClock _clock;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRequestValidator<CreateOrderCommand> _validator;

    public CreateOrderHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IRequestValidator<CreateOrderCommand> validator)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<CreateOrderResult> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(command);
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
}
