using Company.Product.Application.Abstractions.Persistence;
using Company.Product.Application.Common;

namespace Company.Product.Application.UseCases.Orders.GetOrderById;

public interface IGetOrderByIdHandler
{
    Task<GetOrderByIdResult> HandleAsync(GetOrderByIdQuery query, CancellationToken cancellationToken);
}

public sealed class GetOrderByIdHandler : IGetOrderByIdHandler
{
    private readonly IOrderReadService _orderReadService;

    public GetOrderByIdHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public async Task<GetOrderByIdResult> HandleAsync(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        if (query.OrderId == Guid.Empty)
        {
            throw new ApplicationValidationException(new Dictionary<string, string[]>
            {
                [nameof(query.OrderId)] = ["OrderId is required."]
            });
        }

        var order = await _orderReadService.GetByIdAsync(query.OrderId, cancellationToken);
        if (order is null)
        {
            throw new ResourceNotFoundException("Order not found.");
        }

        return new GetOrderByIdResult(order);
    }
}
