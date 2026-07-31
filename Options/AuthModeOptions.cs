namespace DigifyCXIntranet.Options;

public class AuthModeOptions
{
    public const string SectionName = "AuthMode";

    public bool UseWindowsAuthenticationInNonDevelopment { get; set; } = true;
    public bool AllowInsecureHttpForInternalTest { get; set; }
    public bool SeedConfiguredTestUsers { get; set; }
    public List<DevelopmentUserOption> DevelopmentUsers { get; set; } = new();
}

public class DevelopmentUserOption
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Agent";
}
