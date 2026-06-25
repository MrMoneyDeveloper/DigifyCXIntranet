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
        _serviceProvider   = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
        _options           = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation("[UserRegistrySync] Sync worker is disabled by configuration.");
            return;
        }

        // Brief startup delay so the app is fully initialised before the first call
        await Task.Delay(TimeSpan.FromSeconds(options.InitialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // ── Wait until the configured target time-of-day (UTC) ────────────
            var delay = ComputeDelayUntilNextRun(_options.CurrentValue);
            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation(
                    "[UserRegistrySync] Next sync in {Minutes:F0} minutes (target UTC time: {Target}).",
                    delay.TotalMinutes, _options.CurrentValue.SyncTimeUtc);
                await Task.Delay(delay, stoppingToken);
            }

            await RunSyncAsync(stoppingToken);

            // After the run, sleep for the full interval before checking again
            await Task.Delay(
                TimeSpan.FromHours(_options.CurrentValue.SyncIntervalHours),
                stoppingToken);
        }
    }

    // ------------------------------------------------------------------
    // Computes milliseconds to wait until the next SyncTimeUtc window.
    // Returns 0 if SyncTimeUtc is not set (run immediately).
    // ------------------------------------------------------------------
    private static TimeSpan ComputeDelayUntilNextRun(UserRegistrySyncOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.SyncTimeUtc)) return TimeSpan.Zero;
        if (!TimeSpan.TryParse(opts.SyncTimeUtc, out var targetTime)) return TimeSpan.Zero;

        var now = DateTime.UtcNow;
        var targetToday = now.Date.Add(targetTime);
        var next = targetToday <= now ? targetToday.AddDays(1) : targetToday;

        return next - now;
    }

    // ------------------------------------------------------------------
    // Core sync: create new users from sheet, delete users absent from sheet.
    // ------------------------------------------------------------------
    private async Task RunSyncAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("[UserRegistrySync] Starting sync from Google Sheet...");

            using var scope       = _serviceProvider.CreateScope();
            var dbContext         = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager       = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var options           = _options.CurrentValue;

            var client   = _httpClientFactory.CreateClient(nameof(UserRegistrySyncWorker));
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
                _logger.LogWarning("[UserRegistrySync] Unexpected response format — expected a JSON array.");
                return;
            }

            var externalUsers = JsonSerializer.Deserialize<List<ExternalUserDto>>(content);
            if (externalUsers == null || externalUsers.Count == 0)
            {
                _logger.LogInformation("[UserRegistrySync] No employees returned from sheet — skipping to avoid accidental mass-delete.");
                return;
            }

            // Build the canonical normalised-name set from the sheet
            var sheetNormalisedNames = externalUsers
                .Where(x => !string.IsNullOrWhiteSpace(x.fullName))
                .Select(x => NormalizeUsername(x.fullName))
                .ToHashSet(StringComparer.Ordinal);

            // ── CREATION: add any new names that don't exist in the DB yet ────
            var existingUsers = await dbContext.Users
                .OfType<ApplicationUser>()
                .ToDictionaryAsync(u => u.NormalizedUserName ?? string.Empty, StringComparer.Ordinal, stoppingToken);

            var created = 0;
            var updated = 0;

            foreach (var extUser in externalUsers)
            {
                if (string.IsNullOrWhiteSpace(extUser.fullName)) continue;

                var fullName           = extUser.fullName.Trim();
                var generatedUsername  = fullName.Replace(" ", ".").ToLowerInvariant();
                var normalizedUsername = NormalizeUsername(fullName);

                if (existingUsers.TryGetValue(normalizedUsername, out var existingUser))
                {
                    if (existingUser.DisplayName != fullName)
                    {
                        existingUser.DisplayName = fullName;
                        updated++;
                    }
                    continue;
                }

                var newUser = new ApplicationUser
                {
                    UserName           = generatedUsername,
                    NormalizedUserName = normalizedUsername,
                    Email              = $"{generatedUsername}@digifycx.internal",
                    NormalizedEmail    = $"{generatedUsername}@digifycx.internal".ToUpperInvariant(),
                    DisplayName        = fullName,
                    CustomRole         = "Employee",
                    IsFirstTimeLogin   = true,
                    PersonalEmail      = null,
                    EmailConfirmed     = true,
                    SecurityStamp      = Guid.NewGuid().ToString()
                };

                var result = await userManager.CreateAsync(newUser);
                if (result.Succeeded)
                {
                    created++;
                    existingUsers[normalizedUsername] = newUser;
                    _logger.LogInformation("[UserRegistrySync] Created user: {Username}", generatedUsername);
                }
                else
                {
                    _logger.LogWarning("[UserRegistrySync] Failed to create {Username}: {Errors}",
                        generatedUsername,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            // ── DELETION: remove DB accounts no longer in the sheet ───────────
            var deleted = 0;
            if (options.DeleteRemovedUsers)
            {
                // Only consider accounts created by the sync worker (no PersonalEmail
                // set means never manually created / is a sync-managed account).
                // We never delete dev/hardcoded accounts (they have no NormalizedUserName
                // matching sheet names anyway).
                var usersToDelete = existingUsers.Values
                    .Where(u => !sheetNormalisedNames.Contains(u.NormalizedUserName ?? string.Empty))
                    .ToList();

                foreach (var staleUser in usersToDelete)
                {
                    var deleteResult = await userManager.DeleteAsync(staleUser);
                    if (deleteResult.Succeeded)
                    {
                        deleted++;
                        _logger.LogInformation(
                            "[UserRegistrySync] Deleted user no longer in sheet: {Username}",
                            staleUser.UserName);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[UserRegistrySync] Failed to delete stale user {Username}: {Errors}",
                            staleUser.UserName,
                            string.Join(", ", deleteResult.Errors.Select(e => e.Description)));
                    }
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation(
                "[UserRegistrySync] Sync complete. Created: {Created}, Updated: {Updated}, Deleted: {Deleted}.",
                created, updated, deleted);
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
        => fullName.Trim().Replace(" ", ".").ToUpperInvariant();
}

public class ExternalUserDto
{
    public string fullName { get; set; } = string.Empty;
}
