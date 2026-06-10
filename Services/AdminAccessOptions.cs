namespace DigifyCXIntranet.Services;

public class AdminAccessOptions
{
    public const string SectionName = "AdminAccess";

    public List<string> AllowedUsers { get; set; } = new();
    public List<UserRoleAssignment> UserRoles { get; set; } = new();
}

public class UserRoleAssignment
{
    public string Username { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
