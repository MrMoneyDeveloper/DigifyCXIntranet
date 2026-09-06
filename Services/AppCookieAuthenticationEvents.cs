using System.Security.Claims;
using DigifyCXIntranet.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DigifyCXIntranet.Services;

public sealed class AppCookieAuthenticationEvents : CookieAuthenticationEvents
{
    internal const long ValidationCacheSizeLimit = 10_000;
    private static readonly TimeSpan ValidationCacheDuration = TimeSpan.FromMinutes(2);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AppCookieAuthenticationEvents> _logger;
    private readonly IWebHostEnvironment _environment;

    public AppCookieAuthenticationEvents(
        ApplicationDbContext db,
        IMemoryCache cache,
        ILogger<AppCookieAuthenticationEvents> logger,
        IWebHostEnvironment environment)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
        _environment = environment;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var source = principal?.FindFirstValue(AppAuthenticationClaims.AuthenticationSource);
        if (source == AppAuthenticationClaims.DevelopmentUser && _environment.IsDevelopment())
        {
            return;
        }

        if (principal is null || source != AppAuthenticationClaims.IdentityDatabase)
        {
            await RejectAsync(context, principal?.FindFirstValue(ClaimTypes.NameIdentifier),
                "UnsupportedAuthenticationSource");
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var securityStamp = principal.FindFirstValue(AppAuthenticationClaims.SecurityStamp);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(securityStamp) ||
            string.IsNullOrWhiteSpace(role))
        {
            await RejectAsync(context, userId, "MissingSessionClaims");
            return;
        }

        var cacheKey = $"auth-session:{userId}:{securityStamp}:{role}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            return;
        }

        var currentUser = await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.SecurityStamp,
                user.IsFirstTimeLogin,
                user.CustomRole
            })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (!IsCurrentSession(
                currentUser?.SecurityStamp,
                currentUser?.IsFirstTimeLogin,
                currentUser?.CustomRole,
                securityStamp,
                role))
        {
            await RejectAsync(context, userId, "SessionCredentialsChanged");
            return;
        }

        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ValidationCacheDuration,
            Size = 1
        });
    }

    internal static bool IsCurrentSession(
        string? currentSecurityStamp,
        bool? isFirstTimeLogin,
        string? currentRole,
        string cookieSecurityStamp,
        string cookieRole)
    {
        return isFirstTimeLogin == false &&
               !string.IsNullOrWhiteSpace(currentSecurityStamp) &&
               string.Equals(currentSecurityStamp, cookieSecurityStamp, StringComparison.Ordinal) &&
               string.Equals(
                   AppRoles.NormalizeOrEmployee(currentRole),
                   AppRoles.NormalizeOrEmployee(cookieRole),
                   StringComparison.Ordinal);
    }

    private async Task RejectAsync(
        CookieValidatePrincipalContext context,
        string? userId,
        string reason)
    {
        _logger.LogWarning(
            "Authentication cookie rejected for user {UserId}. Reason={Reason}",
            userId ?? "unknown",
            reason);
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
