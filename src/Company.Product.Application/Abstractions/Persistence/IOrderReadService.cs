using Company.Product.Application.Common;

namespace Company.Product.Application.Abstractions.Persistence;

public interface IOrderReadService
{
    Task<OrderReadModel?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<PagedResult<OrderListItemReadModel>> GetOrdersAsync(OrderListQuery query, CancellationToken cancellationToken);
}
