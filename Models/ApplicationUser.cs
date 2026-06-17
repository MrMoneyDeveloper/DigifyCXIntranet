using Microsoft.AspNetCore.Identity;

namespace DigifyCXIntranet.Models;

public class ApplicationUser : IdentityUser
{
    // Inherits Id, UserName, Email, PasswordHash, etc., from IdentityUser
    public string DisplayName { get; set; } = string.Empty;
    public string CustomRole { get; set; } = "Employee";
    public bool IsFirstTimeLogin { get; set; } = true;
    /// <summary>Personal (private) email collected during first-time account activation.</summary>
    public string? PersonalEmail { get; set; }
}
