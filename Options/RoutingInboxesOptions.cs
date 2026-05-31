namespace DigifyCXIntranet.Options;

public class RoutingInboxesOptions
{
    public const string SectionName = "RoutingInboxes";

    public string CanteenInbox { get; set; } = "canteen@digifycx.local";
    public string HrHiringInbox { get; set; } = "hiring@digifycx.local";
    public string HrReferralInbox { get; set; } = "referrals@digifycx.local";
    public string HrReferralsInbox
    {
        get => HrReferralInbox;
        set => HrReferralInbox = value;
    }
    public string HrPayrollInbox { get; set; } = "payroll@digifycx.local";
}
