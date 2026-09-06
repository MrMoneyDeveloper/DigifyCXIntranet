using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class CanteenBatchingOptions
{
    public const string SectionName = "CanteenBatching";

    [Required]
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";

    [Required]
    public string BreakfastCron { get; set; } = "0 0 10 ? * *";

    [Required]
    public string LunchCron { get; set; } = "0 0 13 ? * *";
}
