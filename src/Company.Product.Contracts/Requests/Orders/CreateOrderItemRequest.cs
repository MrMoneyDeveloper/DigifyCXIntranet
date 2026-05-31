using System.ComponentModel.DataAnnotations;

namespace Company.Product.Contracts.Requests.Orders;

public sealed class CreateOrderItemRequest
{
    [Required]
    [MaxLength(64)]
    public string Sku { get; init; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Range(0.01, 9999999)]
    public decimal UnitPrice { get; init; }

    [Range(1, 9999)]
    public int Quantity { get; init; }
}
