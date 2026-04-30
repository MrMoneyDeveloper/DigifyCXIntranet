using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigifyCXIntranet.Pages.Profile;

public class IndexModel : PageModel
{
    public string DisplayName { get; private set; } = string.Empty;
    public string IdentityName { get; private set; } = string.Empty;

    public void OnGet()
    {
        IdentityName = User.Identity?.Name ?? "Unknown";
        DisplayName = UserNameHelper.GetShortName(User);
    }
}
