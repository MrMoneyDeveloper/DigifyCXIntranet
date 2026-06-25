using DigifyCXIntranet.BackgroundJobs;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using System.Threading.RateLimiting;

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
builder.Services.Configure<ActivationOptions>(
    builder.Configuration.GetSection(ActivationOptions.SectionName));

// ── Zendesk inbound webhook (password reset trigger from IT) ──────────────
builder.Services.Configure<ZendeskWebhookOptions>(
    builder.Configuration.GetSection("ZendeskWebhook"));

builder.Services.AddOptions<TechNewsOptions>()
    .Bind(builder.Configuration.GetSection(TechNewsOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
        "TechNews:BaseUrl must be an absolute HTTPS URL.")
    .ValidateOnStart();
builder.Services.AddOptions<UserRegistrySyncOptions>()
    .Bind(builder.Configuration.GetSection(UserRegistrySyncOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !options.Enabled || Uri.TryCreate(options.SheetApiUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
        "UserRegistrySync:SheetApiUrl must be an absolute HTTPS URL when sync is enabled.")
    .ValidateOnStart();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString()
            ?? context.User.Identity?.Name
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 240,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

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

builder.Services.AddHttpClient(nameof(HackerNewsCacheService), (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TechNewsOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 60));
});
builder.Services.AddHttpClient(nameof(ZendeskPolicySyncService), (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ZendeskSyncOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 90));
});
builder.Services.AddHttpClient(nameof(ZendeskTicketService), (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ZendeskSyncOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 90));
});
builder.Services.AddHttpClient(nameof(UserRegistrySyncWorker), (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<UserRegistrySyncOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
});

builder.Services.AddHostedService<TechNewsRefreshHostedService>();
builder.Services.AddHostedService<ZendeskPolicySyncHostedService>();
builder.Services.AddHostedService<MonthlyPayrollHostedService>();
builder.Services.AddHostedService<UserRegistrySyncWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("sqlserver");

// ── API controllers (used by Zendesk webhook) ─────────────────────────────
builder.Services.AddControllers();

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
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
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
    options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/Account/ResetPassword");
    if (!useWindowsAuth)
    {
        options.Conventions.AllowAnonymousToPage("/Account/Login");
        options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
        options.Conventions.AllowAnonymousToPage("/Account/Logout");
        options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
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
SqlServerConnectionSecurity.Validate(connectionString, app.Environment, app.Logger);

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
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

// ── Map API controllers (Zendesk webhook lives here) ──────────────────────
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseStartup");

    await SqlServerStartupValidator.EnsureServerIsReachableAsync(connectionString, startupLogger);

    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseSchemaRepair.EnsurePostMigrationSchemaAsync(db);
    await SeedData.InitializeAsync(db);
}

app.Run();
