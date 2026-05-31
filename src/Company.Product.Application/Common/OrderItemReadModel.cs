namespace Company.Product.Application.Common;

public sealed record OrderItemReadModel(Guid Id, string Sku, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);
