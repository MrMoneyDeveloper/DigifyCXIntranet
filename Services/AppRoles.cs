namespace DigifyCXIntranet.Services;

public static class AppRoles
{
    public const string Employee = "Employee";
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

    public static readonly string[] EmployeeAssignableRoles =
    [
        Employee,
        HrAdmin,
        CanteenAdmin,
        FinanceAdmin
    ];

    public static readonly string[] SuperAdminAssignableRoles =
    [
        Employee,
        HrAdmin,
        CanteenAdmin,
        FinanceAdmin,
        SystemAdmin
    ];

    public static bool TryNormalize(string? role, out string normalizedRole)
    {
        normalizedRole = Employee;
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var match = new[]
        {
            Employee,
            FinanceAdmin,
            HrAdmin,
            CanteenAdmin,
            SystemAdmin,
            SuperAdmin
        }.FirstOrDefault(candidate => string.Equals(candidate, role.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        normalizedRole = match;
        return true;
    }

    public static string NormalizeOrEmployee(string? role)
    {
        return TryNormalize(role, out var normalizedRole) ? normalizedRole : Employee;
    }
}
