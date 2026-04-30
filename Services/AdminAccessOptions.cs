namespace DigifyCXIntranet.Services;

public class AdminAccessOptions
{
    public const string SectionName = "AdminAccess";

    public List<string> AllowedUsers { get; set; } = new();
}
