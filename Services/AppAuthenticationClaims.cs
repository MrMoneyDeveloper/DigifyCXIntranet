using System.Security.Claims;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DigifyCXIntranet.Services;

public static class AppAuthenticationClaims
{
    public const string AuthenticationSource = "digifycx:authentication_source";
    public const string IdentityDatabase = "identity-database";
    public const string DevelopmentUser = "development-user";
    public const string SecurityStamp = "AspNet.Identity.SecurityStamp";

    public static ClaimsPrincipal CreateDatabasePrincipal(
        ApplicationUser user,
        string fallbackUsername)
    {
        var username = user.UserName ?? fallbackUsername;
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? username : user.DisplayName;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.GivenName, displayName),
            new(ClaimTypes.Role, AppRoles.NormalizeOrEmployee(user.CustomRole)),
            new(SecurityStamp, user.SecurityStamp ?? string.Empty),
            new(AuthenticationSource, IdentityDatabase)
        };

        return CreatePrincipal(claims);
    }

    public static ClaimsPrincipal CreateDevelopmentPrincipal(
        string username,
        string displayName,
        string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.GivenName, displayName),
            new(ClaimTypes.Role, role),
            new(AuthenticationSource, DevelopmentUser)
        };

        return CreatePrincipal(claims);
    }

    private static ClaimsPrincipal CreatePrincipal(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
