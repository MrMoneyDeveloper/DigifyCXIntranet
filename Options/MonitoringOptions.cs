using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    [MaxLength(120)]
    public string EventLogSourceName { get; set; } = "DigifyCXIntranet";

    [Range(1, 168)]
    public int CriticalJobMaxAgeHours { get; set; } = 26;

    [Range(50, 100)]
    public int RuntimeMemoryLoadWarningPercent { get; set; } = 90;

    [Range(1, 100000)]
    public int RuntimeThreadPoolQueueWarningLength { get; set; } = 500;
}
