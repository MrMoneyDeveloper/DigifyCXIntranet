using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class ConfigurationRoleClaimsTransformation : IClaimsTransformation
{
    private readonly Dictionary<string, HashSet<string>> _configuredRoles;

    public ConfigurationRoleClaimsTransformation(IOptions<AdminAccessOptions> options)
    {
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

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            principal.Identity is not ClaimsIdentity identity)
        {
            return Task.FromResult(principal);
        }

        var identityName = principal.Identity.Name ?? principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(identityName) ||
            !_configuredRoles.TryGetValue(Normalize(identityName), out var roles))
        {
            return Task.FromResult(principal);
        }

        foreach (var role in roles)
        {
            if (!principal.IsInRole(role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.FromResult(principal);
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
