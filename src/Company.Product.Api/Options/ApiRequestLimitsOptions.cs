using System.ComponentModel.DataAnnotations;

namespace Company.Product.Api.Options;

public sealed class ApiRequestLimitsOptions
{
    public const string SectionName = "RequestLimits";

    [Range(1024, 10 * 1024 * 1024)]
    public long MaxRequestBodyBytes { get; init; } = 1024 * 1024;
}
