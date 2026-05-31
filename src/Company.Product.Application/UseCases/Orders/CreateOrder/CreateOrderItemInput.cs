namespace Company.Product.Application.UseCases.Orders.CreateOrder;

public sealed record CreateOrderItemInput(string Sku, string Name, decimal UnitPrice, int Quantity);
