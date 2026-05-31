namespace Company.Product.Application.Common;

public sealed record OrderListItemReadModel(
    Guid Id,
    Guid CustomerId,
    DateTime CreatedUtc,
    string Status,
    decimal TotalAmount);
