using System.ComponentModel.DataAnnotations;

namespace Company.Product.Api.Options;

public sealed class ApiRateLimitOptions
{
    public const string SectionName = "RateLimits";

    [Range(1, 10000)]
    public int GlobalPermitLimit { get; init; } = 120;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    [Range(1, 1000)]
    public int OrdersWritePermitLimit { get; init; } = 30;
}
