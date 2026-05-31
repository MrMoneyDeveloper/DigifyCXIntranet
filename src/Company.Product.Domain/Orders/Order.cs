using Company.Product.Domain.Common;

namespace Company.Product.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    private Order(Guid id, Guid customerId, DateTime createdUtc, IReadOnlyCollection<OrderItem> items)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("ORDER_CUSTOMER_REQUIRED", "Customer identifier is required.");
        }

        if (items.Count == 0)
        {
            throw new DomainException("ORDER_ITEMS_REQUIRED", "At least one order item is required.");
        }

        Id = id;
        CustomerId = customerId;
        CreatedUtc = createdUtc;
        Status = OrderStatus.Draft;

        foreach (var item in items)
        {
            _items.Add(item);
        }

        RecalculateTotal();
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Order Create(Guid customerId, DateTime createdUtc, IReadOnlyCollection<OrderItem> items)
    {
        return new Order(Guid.NewGuid(), customerId, createdUtc, items);
    }

    public void MarkPaid()
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new DomainException("ORDER_INVALID_STATE", "Cancelled orders cannot be paid.");
        }

        Status = OrderStatus.Paid;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("ORDER_INVALID_STATE", "Paid orders cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }

    private void RecalculateTotal()
    {
        TotalAmount = decimal.Round(_items.Sum(i => i.LineTotal), 2, MidpointRounding.AwayFromZero);
    }
}
