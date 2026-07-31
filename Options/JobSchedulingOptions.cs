using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class JobSchedulingOptions
{
    public const string SectionName = "JobScheduling";

    public bool UsePersistentStore { get; set; } = false;
    public bool UseClustering { get; set; } = false;

    [Range(0, 300)]
    public int StartupDelaySeconds { get; set; } = 15;

    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    [Range(1, 1440)]
    public int RetryDelayMinutes { get; set; } = 5;

    [Range(1, 1440)]
    public int TechNewsIntervalMinutes { get; set; } = 60;

    [Range(1, 168)]
    public int ZendeskPolicySyncIntervalHours { get; set; } = 24;

    [Range(1, 168)]
    public int UserRegistrySyncIntervalHours { get; set; } = 24;

    [RegularExpression(@"^\S+(\s+\S+){5,6}$")]
    public string PayrollCron { get; set; } = "0 0 0 25 * ?";

    [Range(1, 60)]
    public int EmailDispatchIntervalMinutes { get; set; } = 1;

    [Range(1, 100)]
    public int EmailDispatchBatchSize { get; set; } = 20;
}
