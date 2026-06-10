namespace DigifyCXIntranet.Services;

public static class AppRoles
{
    public const string Agent = "Agent";
    public const string FinanceAdmin = "FinanceAdmin";
    public const string HrAdmin = "HrAdmin";
    public const string CanteenAdmin = "CanteenAdmin";
    public const string SystemAdmin = "SystemAdmin";
    public const string SuperAdmin = "SuperAdmin";

    public static readonly string[] AdminRoles =
    [
        FinanceAdmin,
        HrAdmin,
        CanteenAdmin,
        SystemAdmin,
        SuperAdmin
    ];

    public static readonly string[] SystemWideAdminRoles =
    [
        SystemAdmin,
        SuperAdmin
    ];
}
