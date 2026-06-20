using System.Text.Json;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class UserRegistrySyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserRegistrySyncWorker> _logger;
    private readonly IOptionsMonitor<UserRegistrySyncOptions> _options;

    public UserRegistrySyncWorker(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<UserRegistrySyncWorker> logger,
        IOptionsMonitor<UserRegistrySyncOptions> options)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation("[UserRegistrySync] Sync worker is disabled by configuration.");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(options.InitialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(_options.CurrentValue.SyncIntervalHours), stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("[UserRegistrySync] Starting sync from Google Sheet...");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var options = _options.CurrentValue;

            var client = _httpClientFactory.CreateClient(nameof(UserRegistrySyncWorker));
            var response = await client.GetAsync(options.SheetApiUrl, stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[UserRegistrySync] Sheet API returned {StatusCode}", response.StatusCode);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(stoppingToken);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("[UserRegistrySync] Unexpected response format.");
                return;
            }

            var externalUsers = JsonSerializer.Deserialize<List<ExternalUserDto>>(content);
            if (externalUsers == null || externalUsers.Count == 0)
            {
                _logger.LogInformation("[UserRegistrySync] No employees returned from sheet.");
                return;
            }

            var normalizedNames = externalUsers
                .Where(x => !string.IsNullOrWhiteSpace(x.fullName))
                .Select(x => NormalizeUsername(x.fullName))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var existingUsers = await dbContext.Users
                .OfType<ApplicationUser>()
                .Where(user => user.NormalizedUserName != null && normalizedNames.Contains(user.NormalizedUserName))
                .ToDictionaryAsync(user => user.NormalizedUserName!, StringComparer.Ordinal, stoppingToken);

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

                var result = await userManager.CreateAsync(newUser);
                if (result.Succeeded)
                {
                    created++;
                    existingUsers[normalizedUsername] = newUser;
                    _logger.LogInformation("[UserRegistrySync] Created user: {Username}", generatedUsername);
                    continue;
                }

                _logger.LogWarning("[UserRegistrySync] Failed to create {Username}: {Errors}",
                    generatedUsername,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("[UserRegistrySync] Sync complete. Created: {Created}, Updated: {Updated}, Already existed: {Skipped}",
                created, updated, skipped);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[UserRegistrySync] Sync worker is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserRegistrySync] Sync failed with exception.");
        }
    }

    private static string NormalizeUsername(string fullName)
    {
        return fullName.Trim().Replace(" ", ".").ToUpperInvariant();
    }
}

public class ExternalUserDto
{
    public string fullName { get; set; } = string.Empty;
}
