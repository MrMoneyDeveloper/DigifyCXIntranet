using Company.Product.Application.UseCases.Orders.CreateOrder;
using FluentAssertions;

namespace Company.Product.Application.Tests.UseCases.Orders.CreateOrder;

public sealed class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_ReturnsNoErrors_ForValidCommand()
    {
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            [new CreateOrderItemInput("SKU-1", "Keyboard", 100m, 1)]);

        var errors = _validator.Validate(command);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReturnsItemErrors_ForInvalidItems()
    {
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            [new CreateOrderItemInput("", "Keyboard", 100m, 1)]);

        var errors = _validator.Validate(command);

        errors.Should().ContainKey(nameof(command.Items));
    }
}
