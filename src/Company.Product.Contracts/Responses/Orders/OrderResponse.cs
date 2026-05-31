namespace Company.Product.Contracts.Responses.Orders;

public sealed class OrderResponse
{
    public required Guid Id { get; init; }

    public required Guid CustomerId { get; init; }

    public required DateTime CreatedUtc { get; init; }

    public required string Status { get; init; }

    public required decimal TotalAmount { get; init; }

    public required IReadOnlyList<OrderItemResponse> Items { get; init; }
}
