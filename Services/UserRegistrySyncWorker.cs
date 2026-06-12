using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
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

    // Your production-ready deployed macro URL
    private const string ApiUrl = "https://script.google.com/macros/s/AKfycbxrrhzqV_pFVYpUH-vv2i7EUA5x7i184HokCBVdUxNJVe49r6RBwxI24S2ZVauUo9A5Zg/exec";

    public UserRegistrySyncWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Short initial delay on startup to ensure system services are fully ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(ApiUrl, stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(stoppingToken);

                    // Parse safely using JsonDocument to handle the array payload smoothly
                    using JsonDocument doc = JsonDocument.Parse(content);
                    JsonElement root = doc.RootElement;

                    List<ExternalUserDto> externalUsers = new List<ExternalUserDto>();

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        externalUsers = JsonSerializer.Deserialize<List<ExternalUserDto>>(content);
                    }

                    if (externalUsers != null && externalUsers.Count > 0)
                    {
                        foreach (var extUser in externalUsers)
                        {
                            if (string.IsNullOrWhiteSpace(extUser.fullName)) continue;

                            // Standardize format to first.last
                            var generatedUsername = extUser.fullName.Replace(" ", ".").ToLower();

                            // Prevent duplicate creation loops
                            var existingUser = dbContext.Users.FirstOrDefault(u => u.NormalizedUserName == generatedUsername.ToUpper());

                            if (existingUser == null)
                            {
                                var newUser = new ApplicationUser
                                {
                                    UserName = generatedUsername,
                                    NormalizedUserName = generatedUsername.ToUpper(),
                                    DisplayName = extUser.fullName,
                                    CustomRole = "Employee",
                                    IsFirstTimeLogin = true,
                                    PasswordHash = "AQAAAAIAAYagAAAAEOf12WelcomePlaceholderHashMatch=="
                                };

                                dbContext.Users.Add(newUser);
                            }
                        }
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // Core loop safety net: logs can be added here if needed
            }

            // 🕒 PRODUCTION PARAMETER: Wait exactly 24 hours before checking the registry sheet again
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

public class ExternalUserDto
{
    public string fullName { get; set; } = string.Empty;
}