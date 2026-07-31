using System.Text.Json;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class UserRegistrySyncService : IUserRegistrySyncService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<UserRegistrySyncOptions> _options;
    private readonly ILogger<UserRegistrySyncService> _logger;

    public UserRegistrySyncService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<UserRegistrySyncOptions> options,
        ILogger<UserRegistrySyncService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<UserRegistrySyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation("[UserRegistrySync] Sync is disabled by configuration.");
            return new UserRegistrySyncResult(0, 0, 0);
        }

        _logger.LogInformation("[UserRegistrySync] Starting sync from Google Sheet.");
        var client = _httpClientFactory.CreateClient(nameof(UserRegistrySyncWorker));
        var response = await client.GetAsync(options.SheetApiUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Sheet API returned {(int)response.StatusCode}.");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Sheet API returned an unexpected response format.");
        }

        var externalUsers = JsonSerializer.Deserialize<List<ExternalUserDto>>(content);
        if (externalUsers == null || externalUsers.Count == 0)
        {
            return new UserRegistrySyncResult(0, 0, 0);
        }

        var normalizedNames = externalUsers
            .Where(x => !string.IsNullOrWhiteSpace(x.fullName))
            .Select(x => NormalizeUsername(x.fullName))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existingUsers = await _dbContext.Users
            .OfType<ApplicationUser>()
            .Where(user => user.NormalizedUserName != null && normalizedNames.Contains(user.NormalizedUserName))
            .ToDictionaryAsync(user => user.NormalizedUserName!, StringComparer.Ordinal, cancellationToken);

        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var extUser in externalUsers)
        {
            if (string.IsNullOrWhiteSpace(extUser.fullName))
            {
                continue;
            }

            var fullName = extUser.fullName.Trim();
            var generatedUsername = fullName.Replace(" ", ".").ToLowerInvariant();
            var normalizedUsername = NormalizeUsername(fullName);

            if (existingUsers.TryGetValue(normalizedUsername, out var existingUser))
            {
                if (existingUser.DisplayName != fullName)
                {
                    existingUser.DisplayName = fullName;
                    updated++;
                }

                skipped++;
                continue;
            }

            var newUser = new ApplicationUser
            {
                UserName = generatedUsername,
                NormalizedUserName = normalizedUsername,
                Email = $"{generatedUsername}@digifycx.internal",
                NormalizedEmail = $"{generatedUsername}@digifycx.internal".ToUpperInvariant(),
                DisplayName = fullName,
                CustomRole = "Employee",
                IsFirstTimeLogin = true,
                PersonalEmail = null,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await _userManager.CreateAsync(newUser);
            if (result.Succeeded)
            {
                created++;
                existingUsers[normalizedUsername] = newUser;
                continue;
            }

            throw new InvalidOperationException($"Failed to create {generatedUsername}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[UserRegistrySync] Sync complete. Created={Created} Updated={Updated} Skipped={Skipped}", created, updated, skipped);
        return new UserRegistrySyncResult(created, updated, skipped);
    }

    private static string NormalizeUsername(string fullName)
    {
        return fullName.Trim().Replace(" ", ".").ToUpperInvariant();
    }

    private class ExternalUserDto
    {
        public string fullName { get; set; } = string.Empty;
    }
}
