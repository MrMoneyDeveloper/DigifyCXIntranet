using Company.Product.Application.Common;

namespace Company.Product.Application.UseCases.Orders.CreateOrder;

public sealed class CreateOrderCommandValidator : IRequestValidator<CreateOrderCommand>
{
    private const int MaxItems = 100;

    public IReadOnlyDictionary<string, string[]> Validate(CreateOrderCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        if (command.CustomerId == Guid.Empty)
        {
            errors[nameof(command.CustomerId)] = ["CustomerId is required."];
        }

        if (command.Items.Count == 0)
        {
            errors[nameof(command.Items)] = ["At least one order item is required."];
            return errors;
        }

        if (command.Items.Count > MaxItems)
        {
            errors[nameof(command.Items)] = [$"A maximum of {MaxItems} items is allowed per order."];
        }

        var itemErrors = command.Items
            .Select((item, index) => new { index, item })
            .Where(entry => string.IsNullOrWhiteSpace(entry.item.Sku)
                || string.IsNullOrWhiteSpace(entry.item.Name)
                || entry.item.UnitPrice <= 0
                || entry.item.Quantity <= 0)
            .Select(entry => $"Items[{entry.index}] is invalid.")
            .ToArray();

        if (itemErrors.Length > 0)
        {
            errors[nameof(command.Items)] = itemErrors;
        }

        return errors;
    }
}
