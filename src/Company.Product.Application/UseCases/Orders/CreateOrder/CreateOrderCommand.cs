namespace Company.Product.Application.UseCases.Orders.CreateOrder;

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyList<CreateOrderItemInput> Items);
