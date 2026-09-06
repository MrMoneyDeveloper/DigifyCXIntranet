using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class PayrollOptions
{
    public const string SectionName = "Payroll";

    [Range(1, 28)]
    public int RunDayOfMonth { get; set; } = 25;

    [Range(0, 23)]
    public int RunHour24 { get; set; } = 0;

    [Required]
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
}
