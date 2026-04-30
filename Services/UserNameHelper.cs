using System.Security.Claims;

namespace DigifyCXIntranet.Services;

public static class UserNameHelper
{
    public static string GetShortName(ClaimsPrincipal user)
    {
        var name = user.Identity?.Name ?? "Unknown";
        if (name.Contains('\\'))
        {
            return name[(name.LastIndexOf('\\') + 1)..];
        }

        return name;
    }
}
