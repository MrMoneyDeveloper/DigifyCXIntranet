namespace Company.Product.Application.UseCases.Orders.ListOrders;

public sealed record ListOrdersQuery(Guid? CustomerId, int Page, int PageSize);
