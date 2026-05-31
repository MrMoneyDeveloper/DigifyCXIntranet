namespace DigifyCXIntranet.Options;

public class PayrollOptions
{
    public const string SectionName = "Payroll";

    public int RunDayOfMonth { get; set; } = 25;
    public int RunHour24 { get; set; } = 0;
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";
}
