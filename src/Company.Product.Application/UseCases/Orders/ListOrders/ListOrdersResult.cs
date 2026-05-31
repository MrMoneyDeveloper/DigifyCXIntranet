using Company.Product.Application.Common;

namespace Company.Product.Application.UseCases.Orders.ListOrders;

public sealed record ListOrdersResult(PagedResult<OrderListItemReadModel> Orders);
