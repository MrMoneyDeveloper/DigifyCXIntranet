using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class RoutingInboxesOptions
{
    public const string SectionName = "RoutingInboxes";

    [Required, EmailAddress]
    public string CanteenInbox { get; set; } = "canteen@digifycx.local";

    [Required, EmailAddress]
    public string HrHiringInbox { get; set; } = "hiring@digifycx.local";

    [Required, EmailAddress]
    public string HrReferralInbox { get; set; } = "referrals@digifycx.local";
    public string HrReferralsInbox
    {
        get => HrReferralInbox;
        set => HrReferralInbox = value;
    }
    [Required, EmailAddress]
    public string HrPayrollInbox { get; set; } = "payroll@digifycx.local";
}
