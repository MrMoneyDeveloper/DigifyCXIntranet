namespace Company.Product.Application.Common;

public sealed record OrderReadModel(
    Guid Id,
    Guid CustomerId,
    DateTime CreatedUtc,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemReadModel> Items);
