using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class ConfigurationAdminAccessService : IAdminAccessService
{
    private readonly HashSet<string> _allowedUsers;

    public ConfigurationAdminAccessService(IOptions<AdminAccessOptions> options)
    {
        _allowedUsers = options.Value.AllowedUsers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAdmin(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var identityName = user.Identity?.Name;
        if (string.IsNullOrWhiteSpace(identityName))
        {
            return false;
        }

        return _allowedUsers.Contains(Normalize(identityName));
    }

    private static string Normalize(string value)
    {
        var candidate = value.Trim();
        if (candidate.Contains('\\'))
        {
            candidate = candidate[(candidate.LastIndexOf('\\') + 1)..];
        }

        return candidate;
    }
}
