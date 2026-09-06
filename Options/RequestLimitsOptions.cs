using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public sealed class RequestLimitsOptions
{
    public const string SectionName = "RequestLimits";

    [Range(1 * 1024 * 1024, 100 * 1024 * 1024)]
    public long MaxRequestBodyBytes { get; set; } = 8 * 1024 * 1024;

    [Range(1 * 1024 * 1024, 100 * 1024 * 1024)]
    public long MaxMultipartBodyBytes { get; set; } = 6 * 1024 * 1024;
}
