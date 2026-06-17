using DigifyCXIntranet.BackgroundJobs;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

void ConfigureSqlServer(DbContextOptionsBuilder options)
{
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 3);
        sql.CommandTimeout(30);
    });
}

builder.Services.AddDbContext<ApplicationDbContext>(ConfigureSqlServer);
builder.Services.AddDbContext<CanteenDbContext>(ConfigureSqlServer);
builder.Services.AddDbContext<HrDbContext>(ConfigureSqlServer);
builder.Services.AddDbContext<PolicyDbContext>(ConfigureSqlServer);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<AdminAccessOptions>(
    builder.Configuration.GetSection(AdminAccessOptions.SectionName));
builder.Services.Configure<AuthModeOptions>(
    builder.Configuration.GetSection(AuthModeOptions.SectionName));
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<RoutingInboxesOptions>(
    builder.Configuration.GetSection(RoutingInboxesOptions.SectionName));
builder.Services.Configure<CanteenBatchingOptions>(
    builder.Configuration.GetSection(CanteenBatchingOptions.SectionName));
builder.Services.Configure<PayrollOptions>(
    builder.Configuration.GetSection(PayrollOptions.SectionName));
builder.Services.Configure<TechNewsOptions>(
    builder.Configuration.GetSection(TechNewsOptions.SectionName));
builder.Services.Configure<HomePageOptions>(
    builder.Configuration.GetSection(HomePageOptions.SectionName));
builder.Services.Configure<ZendeskSyncOptions>(
    builder.Configuration.GetSection(ZendeskSyncOptions.SectionName));
builder.Services.Configure<OutboxOptions>(
    builder.Configuration.GetSection(OutboxOptions.SectionName));

builder.Services.AddSingleton<IAdminAccessService, ConfigurationAdminAccessService>();
builder.Services.AddTransient<IClaimsTransformation, ConfigurationRoleClaimsTransformation>();
builder.Services.AddSingleton<IClock, DigifyCXIntranet.Services.SystemClock>();
builder.Services.AddScoped<ICanteenBatchService, CanteenBatchService>();
builder.Services.AddScoped<IFileExportService, ClosedXmlFileExportService>();
builder.Services.AddScoped<IZendeskPolicySyncService, ZendeskPolicySyncService>();
builder.Services.AddScoped<IZendeskTicketService, ZendeskTicketService>();
builder.Services.AddScoped<IFinanceAuditService, FinanceAuditService>();
builder.Services.AddScoped<IEmailSender, SmtpOrOutboxEmailSender>();
builder.Services.AddSingleton<ITechNewsCacheService, HackerNewsCacheService>();

builder.Services.AddHttpClient(nameof(HackerNewsCacheService));
builder.Services.AddHttpClient(nameof(ZendeskPolicySyncService));
builder.Services.AddHttpClient(nameof(ZendeskTicketService));

builder.Services.AddHttpClient<UserRegistrySyncWorker>();

builder.Services.AddHostedService<TechNewsRefreshHostedService>();
builder.Services.AddHostedService<ZendeskPolicySyncHostedService>();
builder.Services.AddHostedService<MonthlyPayrollHostedService>();
builder.Services.AddHostedService<UserRegistrySyncWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("sqlserver");

var authMode = builder.Configuration.GetSection(AuthModeOptions.SectionName).Get<AuthModeOptions>() ?? new AuthModeOptions();
var useWindowsAuth = !builder.Environment.IsDevelopment() && authMode.UseWindowsAuthenticationInNonDevelopment;

if (useWindowsAuth)
{
    builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
        .AddNegotiate();
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(AppRoles.AdminRoles));
    options.AddPolicy(AppPolicies.AdminConsole, policy =>
        policy.RequireRole(AppRoles.AdminRoles));
    options.AddPolicy(AppPolicies.FinanceLedger, policy =>
        policy.RequireRole(AppRoles.FinanceAdmin, AppRoles.SystemAdmin, AppRoles.SuperAdmin));
    options.AddPolicy(AppPolicies.HrOperations, policy =>
        policy.RequireRole(AppRoles.HrAdmin, AppRoles.SystemAdmin, AppRoles.SuperAdmin));
    options.AddPolicy(AppPolicies.CanteenOperations, policy =>
        policy.RequireRole(AppRoles.CanteenAdmin, AppRoles.SystemAdmin, AppRoles.SuperAdmin));
    options.AddPolicy(AppPolicies.SystemOperations, policy =>
        policy.RequireRole(AppRoles.SystemAdmin, AppRoles.SuperAdmin));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", AppPolicies.AdminConsole);
    options.Conventions.AuthorizePage("/Admin/Menu", AppPolicies.CanteenOperations);
    options.Conventions.AuthorizePage("/Admin/MenuEdit", AppPolicies.CanteenOperations);
    options.Conventions.AuthorizePage("/Admin/Canteen", AppPolicies.CanteenOperations);
    options.Conventions.AuthorizePage("/Admin/Jobs", AppPolicies.HrOperations);
    options.Conventions.AuthorizePage("/Admin/JobEdit", AppPolicies.HrOperations);
    options.Conventions.AuthorizePage("/Admin/Policies", AppPolicies.HrOperations);
    options.Conventions.AuthorizePage("/Admin/PolicyEdit", AppPolicies.HrOperations);
    options.Conventions.AuthorizePage("/Admin/Announcements", AppPolicies.SystemOperations);
    options.Conventions.AuthorizePage("/Admin/AnnouncementEdit", AppPolicies.SystemOperations);
    options.Conventions.AuthorizePage("/Admin/Faq", AppPolicies.SystemOperations);
    options.Conventions.AuthorizePage("/Admin/FaqEdit", AppPolicies.SystemOperations);
    options.Conventions.AuthorizeFolder("/Finance", AppPolicies.FinanceLedger);
    options.Conventions.AllowAnonymousToPage("/External/Apply");
    options.Conventions.AllowAnonymousToPage("/Account/Activate");
    options.Conventions.AllowAnonymousToPage("/Account/ResetPassword");
    if (!useWindowsAuth)
    {
        options.Conventions.AllowAnonymousToPage("/Account/Login");
        options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
        options.Conventions.AllowAnonymousToPage("/Account/Logout");
    }
});

var canteenOptions = builder.Configuration.GetSection(CanteenBatchingOptions.SectionName).Get<CanteenBatchingOptions>() ?? new CanteenBatchingOptions();
TimeZoneInfo batchTimeZone;
try
{
    batchTimeZone = TimeZoneInfo.FindSystemTimeZoneById(canteenOptions.TimeZoneId);
}
catch
{
    batchTimeZone = TimeZoneInfo.Utc;
}

builder.Services.AddQuartz(q =>
{
    var breakfastJobKey = new JobKey(nameof(BreakfastCanteenBatchJob));
    q.AddJob<BreakfastCanteenBatchJob>(opts => opts.WithIdentity(breakfastJobKey));
    q.AddTrigger(opts => opts
        .ForJob(breakfastJobKey)
        .WithIdentity($"{nameof(BreakfastCanteenBatchJob)}-trigger")
        .WithCronSchedule(canteenOptions.BreakfastCron, cron => cron.InTimeZone(batchTimeZone)));

    var lunchJobKey = new JobKey(nameof(LunchCanteenBatchJob));
    q.AddJob<LunchCanteenBatchJob>(opts => opts.WithIdentity(lunchJobKey));
    q.AddTrigger(opts => opts
        .ForJob(lunchJobKey)
        .WithIdentity($"{nameof(LunchCanteenBatchJob)}-trigger")
        .WithCronSchedule(canteenOptions.LunchCron, cron => cron.InTimeZone(batchTimeZone)));
});

builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/database");

app.MapGet("/api/technews", (ITechNewsCacheService cacheService) =>
{
    static string SafeText(string value, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    static string NormalizeUrl(string value, int id)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return uri.ToString();
        return $"https://news.ycombinator.com/item?id={id}";
    }

    var response = new TechNewsResponse
    {
        LastUpdatedUtc = cacheService.LastUpdatedUtc,
        Items = cacheService.GetCurrentItems()
            .Select(x => new TechNewsFeedItem
            {
                Id = x.Id,
                Title = SafeText(x.Title, 180),
                By = SafeText(x.By, 60),
                Url = NormalizeUrl(x.Url, x.Id),
                TimeUnix = x.TimeUnix
            })
            .ToList()
    };
    return Results.Ok(response);
});

app.MapRazorPages();

// ── Startup: DB validation, schema repair, seed, then user registry sync ──
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseStartup");

    await SqlServerStartupValidator.EnsureServerIsReachableAsync(connectionString, startupLogger);

    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseSchemaRepair.EnsurePostMigrationSchemaAsync(db);
    await SeedData.InitializeAsync(db);

    // ── Force immediate user registry sync from Google Sheet on every startup ──
    var syncLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("UserRegistryStartupSync");
    try
    {
        syncLogger.LogInformation("[UserRegistrySync] Starting startup sync from Google Sheet...");

        const string sheetApiUrl =
            "https://script.google.com/macros/s/AKfycbxrrhzqV_pFVYpUH-vv2i7EUA5x7i184HokCBVdUxNJVe49r6RBwxI24S2ZVauUo9A5Zg/exec";

        var http = scope.ServiceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient();

        var sheetResponse = await http.GetAsync(sheetApiUrl);

        if (!sheetResponse.IsSuccessStatusCode)
        {
            syncLogger.LogWarning("[UserRegistrySync] Sheet API returned {Code} — skipping startup seed.",
                sheetResponse.StatusCode);
        }
        else
        {
            var content = await sheetResponse.Content.ReadAsStringAsync();

            // Guard: if the response is HTML (e.g. a Google login redirect), skip
            if (content.TrimStart().StartsWith("<"))
            {
                syncLogger.LogWarning("[UserRegistrySync] Sheet API returned HTML instead of JSON. " +
                    "Ensure the Apps Script is deployed as 'Anyone' (no sign-in required).");
            }
            else
            {
                var externalUsers = System.Text.Json.JsonSerializer
                    .Deserialize<List<ExternalUserDto>>(content);

                if (externalUsers == null || externalUsers.Count == 0)
                {
                    syncLogger.LogInformation("[UserRegistrySync] No users returned from sheet.");
                }
                else
                {
                    var userManager = scope.ServiceProvider
                        .GetRequiredService<UserManager<ApplicationUser>>();

                    int created = 0, updated = 0, skipped = 0;

                    foreach (var ext in externalUsers)
                    {
                        if (string.IsNullOrWhiteSpace(ext.fullName)) continue;

                        var fullName = ext.fullName.Trim();
                        var username = fullName.Replace(" ", ".").ToLower();
                        var normalizedUsername = username.ToUpper();

                        var existing = db.Users
                            .OfType<ApplicationUser>()
                            .FirstOrDefault(u => u.NormalizedUserName == normalizedUsername);

                        if (existing != null)
                        {
                            // Keep DisplayName in sync in case name changed in sheet
                            if (existing.DisplayName != fullName)
                            {
                                existing.DisplayName = fullName;
                                updated++;
                            }
                            else
                            {
                                skipped++;
                            }
                            continue;
                        }

                        var newUser = new ApplicationUser
                        {
                            UserName = username,
                            NormalizedUserName = normalizedUsername,
                            Email = $"{username}@digifycx.internal",
                            NormalizedEmail = $"{username}@digifycx.internal".ToUpper(),
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
                        }
                        else
                        {
                            syncLogger.LogWarning("[UserRegistrySync] Failed to create {Username}: {Errors}",
                                username,
                                string.Join(", ", result.Errors.Select(e => e.Description)));
                        }
                    }

                    await db.SaveChangesAsync();
                    syncLogger.LogInformation(
                        "[UserRegistrySync] Startup sync complete. Created: {Created}, Updated: {Updated}, Unchanged: {Skipped}",
                        created, updated, skipped);
                }
            }
        }
    }
    catch (Exception ex)
    {
        syncLogger.LogError(ex, "[UserRegistrySync] Startup sync failed — app will continue without it.");
    }
}

app.Run();