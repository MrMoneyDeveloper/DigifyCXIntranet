using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public sealed class UserRegistrySyncService : IUserRegistrySyncService
{
    private const string PlaceholderHash =
        "AQAAAAIAAYagAAAAEOf12WelcomeDigifyCX2024!Placeholder";

    private static readonly HashSet<string> SkippedLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Employee Name",
        "Trainee Name",
        "trainee.name"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<UserRegistrySyncOptions> _options;
    private readonly IOptionsMonitor<AuthModeOptions> _authModeOptions;
    private readonly ILogger<UserRegistrySyncService> _logger;

    public UserRegistrySyncService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<UserRegistrySyncOptions> options,
        IOptionsMonitor<AuthModeOptions> authModeOptions,
        ILogger<UserRegistrySyncService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _httpClient = httpClientFactory.CreateClient(nameof(UserRegistrySyncService));
        _options = options;
        _authModeOptions = authModeOptions;
        _logger = logger;
    }

    public async Task<UserRegistrySyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return new UserRegistrySyncResult(0, 0, 0, 0);
        }

        using var response = await _httpClient.GetAsync(options.SheetApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var externalUsers = await JsonSerializer.DeserializeAsync<List<ExternalUserDto>>(
            content,
            cancellationToken: cancellationToken);

        if (externalUsers is null || externalUsers.Count == 0)
        {
            _logger.LogWarning("User registry returned no employees; no database changes were made.");
            return new UserRegistrySyncResult(0, 0, 0, 0);
        }

        var distinctNames = externalUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.FullName) && !IsSkippedLabel(user.FullName))
            .Select(user => user.FullName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var candidates = distinctNames
            .Select(fullName => TryNormalizeRegistryUser(fullName, out var candidate)
                ? candidate
                : null)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();
        var ambiguousUsernames = candidates
            .GroupBy(candidate => candidate.NormalizedUsername, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var validUsers = candidates
            .Where(candidate => !ambiguousUsernames.Contains(candidate.NormalizedUsername))
            .ToList();
        if (validUsers.Count == 0)
        {
            _logger.LogWarning("User registry contained no valid employee rows; no database changes were made.");
            return new UserRegistrySyncResult(0, 0, 0, externalUsers.Count);
        }

        if (ambiguousUsernames.Count > 0)
        {
            _logger.LogWarning(
                "User registry skipped {Count} ambiguous normalized usernames.",
                ambiguousUsernames.Count);
        }

        var sheetUsernames = candidates
            .Select(candidate => candidate.NormalizedUsername)
            .ToHashSet(StringComparer.Ordinal);
        var existingUsers = await _dbContext.Users
            .OfType<ApplicationUser>()
            .Where(user => user.NormalizedUserName != null && user.NormalizedUserName != string.Empty)
            .ToDictionaryAsync(
                user => user.NormalizedUserName!,
                StringComparer.Ordinal,
                cancellationToken);

        var created = 0;
        var updated = 0;
        var deleted = 0;
        var skipped = externalUsers.Count - validUsers.Count;

        foreach (var registryUser in validUsers)
        {
            var fullName = registryUser.DisplayName;
            var normalizedUsername = registryUser.NormalizedUsername;
            if (existingUsers.TryGetValue(normalizedUsername, out var existingUser))
            {
                var changed = false;
                if (!string.Equals(existingUser.DisplayName, fullName, StringComparison.Ordinal))
                {
                    existingUser.DisplayName = fullName;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existingUser.PasswordHash))
                {
                    existingUser.PasswordHash = PlaceholderHash;
                    existingUser.IsFirstTimeLogin = true;
                    changed = true;
                }

                if (changed)
                {
                    updated++;
                }
                else
                {
                    skipped++;
                }

                continue;
            }

            var username = registryUser.Username;
            var newUser = new ApplicationUser
            {
                UserName = username,
                NormalizedUserName = normalizedUsername,
                DisplayName = fullName,
                CustomRole = AppRoles.Employee,
                IsFirstTimeLogin = true,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                PasswordHash = PlaceholderHash
            };
            var result = await _userManager.CreateAsync(newUser);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"User registry could not create {username}: {string.Join(", ", result.Errors.Select(error => error.Code))}");
            }

            created++;
            existingUsers[normalizedUsername] = newUser;
        }

        if (options.DeleteRemovedUsers)
        {
            var protectedUsernames = GetProtectedUsernames();
            var staleUsers = existingUsers.Values
                .Where(user =>
                    !string.IsNullOrWhiteSpace(user.NormalizedUserName) &&
                    AppRoles.NormalizeOrEmployee(user.CustomRole) == AppRoles.Employee &&
                    !sheetUsernames.Contains(user.NormalizedUserName) &&
                    !protectedUsernames.Contains(user.NormalizedUserName))
                .ToList();

            foreach (var staleUser in staleUsers)
            {
                var result = await _userManager.DeleteAsync(staleUser);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"User registry could not remove {staleUser.UserName}: {string.Join(", ", result.Errors.Select(error => error.Code))}");
                }

                deleted++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "User registry sync completed. Created={Created} Updated={Updated} Deleted={Deleted} Skipped={Skipped}",
            created,
            updated,
            deleted,
            skipped);
        return new UserRegistrySyncResult(created, updated, deleted, skipped);
    }

    private HashSet<string> GetProtectedUsernames()
    {
        var authMode = _authModeOptions.CurrentValue;
        if (!authMode.SeedConfiguredTestUsers)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return authMode.DevelopmentUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.Username))
            .Select(user => _userManager.NormalizeName(user.Username.Trim()))
            .Where(username => !string.IsNullOrWhiteSpace(username))
            .Select(username => username!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsSkippedLabel(string fullName) => SkippedLabels.Contains(fullName.Trim());

    internal static bool TryNormalizeRegistryUser(string? value, out RegistryUser? registryUser)
    {
        registryUser = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var displayName = string.Join(
            " ",
            value.Replace("\u00a0", " ").Replace("\u200b", string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (displayName.Length is 0 or > 120 || IsSkippedLabel(displayName))
        {
            return false;
        }

        var username = new StringBuilder(displayName.Length);
        foreach (var character in displayName.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (username.Length > 0 && username[^1] != '.')
                {
                    username.Append('.');
                }
                continue;
            }

            var normalized = char.ToLowerInvariant(character);
            if (char.IsAsciiLetterOrDigit(normalized) || normalized is '-' or '_' or '.' or '@' or '+')
            {
                username.Append(normalized);
            }
        }

        var usernameValue = username.ToString().Trim('.');
        if (usernameValue.Length is 0 or > 256)
        {
            return false;
        }

        registryUser = new RegistryUser(displayName, usernameValue, usernameValue.ToUpperInvariant());
        return true;
    }

    internal sealed record RegistryUser(string DisplayName, string Username, string NormalizedUsername);

    private sealed class ExternalUserDto
    {
        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;
    }
}
