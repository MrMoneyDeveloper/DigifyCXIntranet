using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public sealed class ConfiguredTestUserSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthModeOptions _options;
    private readonly ILogger<ConfiguredTestUserSeeder> _logger;

    public ConfiguredTestUserSeeder(
        UserManager<ApplicationUser> userManager,
        IOptions<AuthModeOptions> options,
        ILogger<ConfiguredTestUserSeeder> logger)
    {
        _userManager = userManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.SeedConfiguredTestUsers)
        {
            return;
        }

        var accounts = ConfiguredTestAccountCatalog.ValidateAndNormalize(_options);
        var created = 0;
        var updated = 0;

        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await _userManager.FindByNameAsync(account.Username);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = account.Username,
                    DisplayName = account.DisplayName,
                    CustomRole = account.Role,
                    IsFirstTimeLogin = false,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };

                EnsureSucceeded(await _userManager.CreateAsync(user, account.Password), "create");
                created++;
                continue;
            }

            var changed = false;
            if (!string.Equals(user.DisplayName, account.DisplayName, StringComparison.Ordinal))
            {
                user.DisplayName = account.DisplayName;
                changed = true;
            }

            if (!string.Equals(user.CustomRole, account.Role, StringComparison.Ordinal))
            {
                user.CustomRole = account.Role;
                changed = true;
            }

            if (user.IsFirstTimeLogin)
            {
                user.IsFirstTimeLogin = false;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(user.SecurityStamp))
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
                changed = true;
            }

            if (changed)
            {
                EnsureSucceeded(await _userManager.UpdateAsync(user), "update profile");
            }

            if (!await _userManager.CheckPasswordAsync(user, account.Password))
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                EnsureSucceeded(
                    await _userManager.ResetPasswordAsync(user, resetToken, account.Password),
                    "synchronize password");
                changed = true;
            }

            if (changed)
            {
                updated++;
            }
        }

        _logger.LogWarning(
            "Internal test account synchronization is enabled. Processed {AccountCount} accounts: {CreatedCount} created and {UpdatedCount} updated. Disable AuthMode:SeedConfiguredTestUsers before final production go-live.",
            accounts.Count,
            created,
            updated);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errorCodes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException(
            $"Unable to {operation} a configured test account. Identity errors: {errorCodes}");
    }
}
