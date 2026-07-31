using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    [MaxLength(120)]
    public string EventLogSourceName { get; set; } = "DigifyCXIntranet";

    [Range(1, 168)]
    public int CriticalJobMaxAgeHours { get; set; } = 26;
}
