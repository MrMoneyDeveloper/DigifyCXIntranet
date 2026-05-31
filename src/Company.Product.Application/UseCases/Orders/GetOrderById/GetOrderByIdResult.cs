using Company.Product.Application.Common;

namespace Company.Product.Application.UseCases.Orders.GetOrderById;

public sealed record GetOrderByIdResult(OrderReadModel Order);
