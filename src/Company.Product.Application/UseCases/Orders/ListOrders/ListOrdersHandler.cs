using Company.Product.Application.Abstractions.Persistence;
using Company.Product.Application.Common;

namespace Company.Product.Application.UseCases.Orders.ListOrders;

public interface IListOrdersHandler
{
    Task<ListOrdersResult> HandleAsync(ListOrdersQuery query, CancellationToken cancellationToken);
}

public sealed class ListOrdersHandler : IListOrdersHandler
{
    private const int MaxPageSize = 200;

    private readonly IOrderReadService _orderReadService;

    public ListOrdersHandler(IOrderReadService orderReadService)
    {
        _orderReadService = orderReadService;
    }

    public async Task<ListOrdersResult> HandleAsync(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 25 : Math.Min(query.PageSize, MaxPageSize);

        var result = await _orderReadService.GetOrdersAsync(new OrderListQuery(query.CustomerId, page, pageSize), cancellationToken);
        return new ListOrdersResult(result);
    }
}
