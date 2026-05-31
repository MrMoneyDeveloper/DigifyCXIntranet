using System.ComponentModel.DataAnnotations;

namespace Company.Product.Contracts.Requests.Orders;

public sealed class CreateOrderRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<CreateOrderItemRequest> Items { get; init; } = [];
}
