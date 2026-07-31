namespace DigifyCXIntranet.BackgroundJobs;

public static class JobNames
{
    public const string BreakfastCanteenBatch = "canteen-breakfast-batch";
    public const string LunchCanteenBatch = "canteen-lunch-batch";
    public const string MonthlyPayroll = "monthly-payroll-export";
    public const string ZendeskPolicySync = "zendesk-policy-sync";
    public const string UserRegistrySync = "user-registry-sync";
    public const string TechNewsRefresh = "tech-news-refresh";
    public const string EmailOutboxDispatch = "email-outbox-dispatch";
}
