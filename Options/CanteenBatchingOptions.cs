namespace DigifyCXIntranet.Options;

public class CanteenBatchingOptions
{
    public const string SectionName = "CanteenBatching";

    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
    public string BreakfastCron { get; set; } = "0 0 10 ? * *";
    public string LunchCron { get; set; } = "0 0 13 ? * *";
}
