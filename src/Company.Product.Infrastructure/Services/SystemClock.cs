using Company.Product.Application.Abstractions.Services;

namespace Company.Product.Infrastructure.Services;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
