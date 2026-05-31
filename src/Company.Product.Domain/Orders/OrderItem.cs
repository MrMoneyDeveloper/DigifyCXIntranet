using Company.Product.Domain.Common;

namespace Company.Product.Domain.Orders;

public sealed class OrderItem
{
    private OrderItem()
    {
        Sku = string.Empty;
        Name = string.Empty;
    }

    public OrderItem(Guid id, string sku, string name, decimal unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("ORDER_ITEM_SKU_REQUIRED", "SKU is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("ORDER_ITEM_NAME_REQUIRED", "Item name is required.");
        }

        if (unitPrice <= 0)
        {
            throw new DomainException("ORDER_ITEM_PRICE_INVALID", "Unit price must be greater than zero.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("ORDER_ITEM_QUANTITY_INVALID", "Quantity must be greater than zero.");
        }

        Id = id;
        Sku = sku.Trim();
        Name = name.Trim();
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
}
