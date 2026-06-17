using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigifyCXIntranet.Services;

public class UserRegistrySyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserRegistrySyncWorker> _logger;

    private const string ApiUrl = "https://script.google.com/macros/s/AKfycbxrrhzqV_pFVYpUH-vv2i7EUA5x7i184HokCBVdUxNJVe49r6RBwxI24S2ZVauUo9A5Zg/exec";

    // 🔧 TESTING: set to seconds. PRODUCTION: switch to FromHours(24)
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);

    public UserRegistrySyncWorker(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<UserRegistrySyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Short initial delay to ensure EF migrations and startup services are ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // ✅ Run IMMEDIATELY on startup, then loop on interval
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncAsync(stoppingToken);
            await Task.Delay(SyncInterval, stoppingToken);
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

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(ApiUrl, stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[UserRegistrySync] Sheet API returned {StatusCode}", response.StatusCode);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(stoppingToken);
            using JsonDocument doc = JsonDocument.Parse(content);

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

            int created = 0;
            int skipped = 0;

            foreach (var extUser in externalUsers)
            {
                if (string.IsNullOrWhiteSpace(extUser.fullName)) continue;

                var fullName = extUser.fullName.Trim();
                var generatedUsername = fullName.Replace(" ", ".").ToLower();

                // Check by NormalizedUserName
                var existingUser = dbContext.Users
                    .OfType<ApplicationUser>()
                    .FirstOrDefault(u => u.NormalizedUserName == generatedUsername.ToUpper());

                if (existingUser != null)
                {
                    // ✅ Ensure DisplayName is always up to date
                    if (existingUser.DisplayName != fullName)
                    {
                        existingUser.DisplayName = fullName;
                    }
                    skipped++;
                    continue;
                }

                // Create new user via UserManager so Identity fields are set correctly
                var newUser = new ApplicationUser
                {
                    UserName = generatedUsername,
                    NormalizedUserName = generatedUsername.ToUpper(),
                    Email = $"{generatedUsername}@digifycx.internal",
                    NormalizedEmail = $"{generatedUsername}@digifycx.internal".ToUpper(),
                    DisplayName = fullName,
                    CustomRole = "Employee",
                    IsFirstTimeLogin = true,
                    PersonalEmail = null,   // ← blank until employee activates
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                // No password set — employee sets it on first activation
                var result = await userManager.CreateAsync(newUser);

                if (result.Succeeded)
                {
                    created++;
                    _logger.LogInformation("[UserRegistrySync] Created user: {Username}", generatedUsername);
                }
                else
                {
                    _logger.LogWarning("[UserRegistrySync] Failed to create {Username}: {Errors}",
                        generatedUsername,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("[UserRegistrySync] Sync complete. Created: {Created}, Already existed: {Skipped}",
                created, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserRegistrySync] Sync failed with exception.");
        }
    }
}

public class ExternalUserDto
{
    public string fullName { get; set; } = string.Empty;
}