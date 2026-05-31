namespace Company.Product.Contracts.Responses.Orders;

public sealed class OrderItemResponse
{
    public required Guid Id { get; init; }

    public required string Sku { get; init; }

    public required string Name { get; init; }

    public required decimal UnitPrice { get; init; }

    public required int Quantity { get; init; }

    public required decimal LineTotal { get; init; }
}
