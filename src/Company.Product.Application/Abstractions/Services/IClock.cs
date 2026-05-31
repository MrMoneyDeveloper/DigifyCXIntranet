namespace Company.Product.Application.Abstractions.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}
