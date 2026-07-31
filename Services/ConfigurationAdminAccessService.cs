using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class ConfigurationAdminAccessService : IAdminAccessService
{
    private readonly HashSet<string> _allowedUsers;
    private readonly Dictionary<string, HashSet<string>> _configuredRoles;

    public ConfigurationAdminAccessService(IOptions<AdminAccessOptions> options)
    {
        _allowedUsers = options.Value.AllowedUsers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _configuredRoles = options.Value.UserRoles
            .Where(x => !string.IsNullOrWhiteSpace(x.Username))
            .GroupBy(x => Normalize(x.Username), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.SelectMany(v => v.Roles)
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(role => role.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAdmin(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.Claims.Any(x =>
                x.Type == ClaimTypes.Role &&
                AppRoles.AdminRoles.Contains(x.Value, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        var identityName = GetIdentityName(user);
        if (string.IsNullOrWhiteSpace(identityName))
        {
            return false;
        }

        return _allowedUsers.Contains(Normalize(identityName));
    }

    public string GetPrimaryRole(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return string.Empty;
        }

        foreach (var role in new[]
                 {
                     AppRoles.SuperAdmin,
                     AppRoles.SystemAdmin,
                     AppRoles.FinanceAdmin,
                     AppRoles.HrAdmin,
                     AppRoles.CanteenAdmin,
                     AppRoles.Employee,
                 })
        {
            if (user.IsInRole(role))
            {
                return role;
            }
        }

        var identityName = GetIdentityName(user);
        if (!string.IsNullOrWhiteSpace(identityName) &&
            _configuredRoles.TryGetValue(Normalize(identityName), out var roles) &&
            roles.Count > 0)
        {
            return roles.First();
        }

        return AppRoles.Employee;
    }

    public IReadOnlyCollection<string> GetConfiguredRoles(string identityName)
    {
        if (string.IsNullOrWhiteSpace(identityName))
        {
            return Array.Empty<string>();
        }

        return _configuredRoles.TryGetValue(Normalize(identityName), out var roles)
            ? roles
            : Array.Empty<string>();
    }

    private static string? GetIdentityName(ClaimsPrincipal user)
    {
        return user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name);
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
