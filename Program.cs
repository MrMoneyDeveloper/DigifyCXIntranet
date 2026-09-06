using DigifyCXIntranet.BackgroundJobs;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Quartz;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
if (OperatingSystem.IsWindows() && !builder.Environment.IsDevelopment())
{
    var eventLogSource = builder.Configuration[$"{MonitoringOptions.SectionName}:EventLogSourceName"];
    ConfigureWindowsEventLog(builder.Logging, eventLogSource);
}
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var dataProtectionOptions = builder.Configuration
    .GetSection(DataProtectionKeyOptions.SectionName)
    .Get<DataProtectionKeyOptions>() ?? new DataProtectionKeyOptions();
var dataProtectionKeyPath = Path.GetFullPath(
    Path.IsPathRooted(dataProtectionOptions.KeyRingPath)
        ? dataProtectionOptions.KeyRingPath
        : Path.Combine(builder.Environment.ContentRootPath, dataProtectionOptions.KeyRingPath));
var webRootPath = Path.GetFullPath(
    builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
if (string.Equals(Path.TrimEndingDirectorySeparator(dataProtectionKeyPath),
        Path.TrimEndingDirectorySeparator(webRootPath), StringComparison.OrdinalIgnoreCase) ||
    dataProtectionKeyPath.StartsWith(
        Path.TrimEndingDirectorySeparator(webRootPath) + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("DataProtection:KeyRingPath must not be inside wwwroot.");
}

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionOptions.ApplicationName)
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
if (dataProtectionOptions.ProtectKeysWithDpapi && OperatingSystem.IsWindows())
{
    dataProtectionBuilder.ProtectKeysWithDpapi();
}

var requestLimits = builder.Configuration
    .GetSection(RequestLimitsOptions.SectionName)
    .Get<RequestLimitsOptions>() ?? new RequestLimitsOptions();
if (requestLimits.MaxMultipartBodyBytes > requestLimits.MaxRequestBodyBytes)
{
    throw new InvalidOperationException("RequestLimits:MaxMultipartBodyBytes cannot exceed MaxRequestBodyBytes.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = requestLimits.MaxRequestBodyBytes;
});
builder.Services.Configure<IISServerOptions>(options =>
    options.MaxRequestBodySize = requestLimits.MaxRequestBodyBytes);
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = requestLimits.MaxMultipartBodyBytes);

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
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 4;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddOptions<AdminAccessOptions>()
    .Bind(builder.Configuration.GetSection(AdminAccessOptions.SectionName));
builder.Services.AddOptions<AuthModeOptions>()
    .Bind(builder.Configuration.GetSection(AuthModeOptions.SectionName));
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host),
        "Smtp:Host is required when SMTP is enabled.")
    .ValidateOnStart();
builder.Services.AddOptions<RoutingInboxesOptions>()
    .Bind(builder.Configuration.GetSection(RoutingInboxesOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<CanteenBatchingOptions>()
    .Bind(builder.Configuration.GetSection(CanteenBatchingOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Quartz.CronExpression.IsValidExpression(options.BreakfastCron) &&
                         Quartz.CronExpression.IsValidExpression(options.LunchCron),
        "Canteen batching schedules must be valid Quartz cron expressions.")
    .Validate(options => IsValidTimeZone(options.TimeZoneId),
        "CanteenBatching:TimeZoneId must identify an installed time zone.")
    .ValidateOnStart();
builder.Services.AddOptions<PayrollOptions>()
    .Bind(builder.Configuration.GetSection(PayrollOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => IsValidTimeZone(options.TimeZoneId),
        "Payroll:TimeZoneId must identify an installed time zone.")
    .ValidateOnStart();
builder.Services.AddOptions<HomePageOptions>()
    .Bind(builder.Configuration.GetSection(HomePageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ZendeskSyncOptions>()
    .Bind(builder.Configuration.GetSection(ZendeskSyncOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => string.IsNullOrWhiteSpace(options.BaseUrl) || IsAbsoluteHttpsUrl(options.BaseUrl),
        "ZendeskSync:BaseUrl must be an absolute HTTPS URL when configured.")
    .Validate(options => !options.UseApiToken ||
                         (!string.IsNullOrWhiteSpace(options.Email) && !string.IsNullOrWhiteSpace(options.ApiToken)),
        "ZendeskSync email and API token are required when API-token authentication is enabled.")
    .ValidateOnStart();
builder.Services.AddOptions<OutboxOptions>()
    .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ActivationOptions>()
    .Bind(builder.Configuration.GetSection(ActivationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SqlServerSecurityOptions>()
    .Bind(builder.Configuration.GetSection(SqlServerSecurityOptions.SectionName));
builder.Services.AddOptions<DataProtectionKeyOptions>()
    .Bind(builder.Configuration.GetSection(DataProtectionKeyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RequestLimitsOptions>()
    .Bind(builder.Configuration.GetSection(RequestLimitsOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.MaxMultipartBodyBytes <= options.MaxRequestBodyBytes,
        "RequestLimits:MaxMultipartBodyBytes cannot exceed MaxRequestBodyBytes.")
    .ValidateOnStart();
builder.Services.AddOptions<BrowserSecurityOptions>()
    .Bind(builder.Configuration.GetSection(BrowserSecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── Zendesk inbound webhook (password reset trigger from IT) ──────────────
builder.Services.AddOptions<ZendeskWebhookOptions>()
    .Bind(builder.Configuration.GetSection("ZendeskWebhook"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RateLimitPoliciesOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitPoliciesOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<BackupHealthOptions>()
    .Bind(builder.Configuration.GetSection(BackupHealthOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.DatabaseName),
        "BackupHealth:DatabaseName is required when backup health monitoring is enabled.")
    .ValidateOnStart();
builder.Services.AddOptions<JobSchedulingOptions>()
    .Bind(builder.Configuration.GetSection(JobSchedulingOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Quartz.CronExpression.IsValidExpression(options.PayrollCron),
        "JobScheduling:PayrollCron must be a valid Quartz cron expression.")
    .Validate(options => !options.UseClustering || options.UsePersistentStore,
        "Quartz clustering requires JobScheduling:UsePersistentStore=true.")
    .ValidateOnStart();
builder.Services.AddOptions<MonitoringOptions>()
    .Bind(builder.Configuration.GetSection(MonitoringOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
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

static bool IsAbsoluteHttpsUrl(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

[SupportedOSPlatform("windows")]
static void ConfigureWindowsEventLog(ILoggingBuilder logging, string? sourceName)
{
#pragma warning disable CA1416 // The caller is guarded by OperatingSystem.IsWindows().
    logging.AddEventLog(settings =>
        settings.SourceName = string.IsNullOrWhiteSpace(sourceName) ? "DigifyCXIntranet" : sourceName);
#pragma warning restore CA1416
}

static bool IsValidTimeZone(string value)
{
    try
    {
        _ = TimeZoneInfo.FindSystemTimeZoneById(value);
        return true;
    }
    catch (TimeZoneNotFoundException)
    {
        return false;
    }
    catch (InvalidTimeZoneException)
    {
        return false;
    }
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
});

var rateLimitOptions = builder.Configuration.GetSection(RateLimitPoliciesOptions.SectionName).Get<RateLimitPoliciesOptions>() ?? new RateLimitPoliciesOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");
        logger.LogWarning("Rate limit rejected {Path} for {RemoteIp}.", httpContext.Request.Path, httpContext.Connection.RemoteIpAddress);
        httpContext.Response.ContentType = "text/plain";
        await httpContext.Response.WriteAsync("Too many requests. Please wait and try again.", cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.User.Identity?.IsAuthenticated == true
            ? $"user:{context.User.Identity.Name ?? "unknown"}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.GlobalPermitLimit,
            Window = TimeSpan.FromSeconds(rateLimitOptions.GlobalWindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    AddFixedPolicy(options, "login", rateLimitOptions.Login);
    AddFixedPolicy(options, "forgot-password", rateLimitOptions.ForgotPassword);
    AddFixedPolicy(options, "account-activation", rateLimitOptions.AccountActivation);
    AddFixedPolicy(options, "password-reset", rateLimitOptions.PasswordReset);
    AddFixedPolicy(options, "external-application", rateLimitOptions.ExternalApplication);
    AddFixedPolicy(options, "zendesk-webhook", rateLimitOptions.ZendeskWebhook);
    AddFixedPolicy(options, "csp-report", rateLimitOptions.CspReport);
});

static void AddFixedPolicy(RateLimiterOptions options, string policyName, EndpointRateLimitOptions policy)
{
    options.AddPolicy(policyName, context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{policyName}:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            Window = TimeSpan.FromMinutes(policy.WindowMinutes),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
}

builder.Services.AddSingleton<IAdminAccessService, ConfigurationAdminAccessService>();
builder.Services.AddSingleton<IIdentifierRateLimiter, IdentifierRateLimiter>();
builder.Services.AddMemoryCache(options =>
    options.SizeLimit = AppCookieAuthenticationEvents.ValidationCacheSizeLimit);
builder.Services.AddScoped<AppCookieAuthenticationEvents>();
builder.Services.AddTransient<IClaimsTransformation, ConfigurationRoleClaimsTransformation>();
builder.Services.AddSingleton<IClock, DigifyCXIntranet.Services.SystemClock>();
builder.Services.AddScoped<ICanteenBatchService, CanteenBatchService>();
builder.Services.AddScoped<IFileExportService, ClosedXmlFileExportService>();
builder.Services.AddScoped<IZendeskPolicySyncService, ZendeskPolicySyncService>();
builder.Services.AddScoped<IZendeskTicketService, ZendeskTicketService>();
builder.Services.AddScoped<IFinanceAuditService, FinanceAuditService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IBackgroundJobRunRecorder, BackgroundJobRunRecorder>();
builder.Services.AddScoped<IEmailSender, SmtpOrOutboxEmailSender>();
builder.Services.AddScoped<IEmailOutboxDispatcher, EmailOutboxDispatcher>();
builder.Services.AddScoped<IUserRegistrySyncService, UserRegistrySyncService>();
builder.Services.AddScoped<IMonthlyPayrollRunner, MonthlyPayrollRunner>();
builder.Services.AddScoped<ConfiguredTestUserSeeder>();
builder.Services.AddSingleton<IZendeskHtmlSanitizer, ZendeskHtmlSanitizer>();
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
builder.Services.AddHttpClient(nameof(UserRegistrySyncService), (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<UserRegistrySyncOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<RuntimeResourceHealthCheck>("runtime-resources", tags: new[] { "ready", "runtime" })
    .AddCheck<DatabaseHealthCheck>("sqlserver", tags: new[] { "ready", "database" })
    .AddCheck<BackupHealthCheck>("backup", tags: new[] { "ready", "database" })
    .AddCheck<CriticalJobHealthCheck>("critical-jobs", tags: new[] { "ready" });

// ── API controllers (used by Zendesk webhook) ─────────────────────────────
builder.Services.AddControllers(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var authMode = builder.Configuration.GetSection(AuthModeOptions.SectionName).Get<AuthModeOptions>() ?? new AuthModeOptions();
var useWindowsAuth = !builder.Environment.IsDevelopment() && authMode.UseWindowsAuthenticationInNonDevelopment;
var allowInsecureHttpForInternalTest =
    !builder.Environment.IsDevelopment() && authMode.AllowInsecureHttpForInternalTest;

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
            options.EventsType = typeof(AppCookieAuthenticationEvents);
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.IsEssential = true;
            options.Cookie.Name = builder.Environment.IsDevelopment() || allowInsecureHttpForInternalTest
                ? "DigifyCX.Auth"
                : "__Host-DigifyCX.Auth";
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || allowInsecureHttpForInternalTest
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });
}

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.Name = builder.Environment.IsDevelopment() || allowInsecureHttpForInternalTest
        ? "DigifyCX.Antiforgery"
        : "__Host-DigifyCX.Antiforgery";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || allowInsecureHttpForInternalTest
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.Configure<CookieTempDataProviderOptions>(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.Name = builder.Environment.IsDevelopment() || allowInsecureHttpForInternalTest
        ? "DigifyCX.TempData"
        : "__Host-DigifyCX.TempData";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || allowInsecureHttpForInternalTest
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

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
    options.AddPolicy(AppPolicies.AnnouncementManagement, policy =>
        policy.RequireRole(AppRoles.HrAdmin));
    options.AddPolicy(AppPolicies.UserManagement, policy =>
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
    options.Conventions.AuthorizePage("/Admin/Announcements", AppPolicies.AnnouncementManagement);
    options.Conventions.AuthorizePage("/Admin/AnnouncementEdit", AppPolicies.AnnouncementManagement);
    options.Conventions.AuthorizePage("/Admin/Faq", AppPolicies.SystemOperations);
    options.Conventions.AuthorizePage("/Admin/FaqEdit", AppPolicies.SystemOperations);
    options.Conventions.AuthorizePage("/Admin/Users", AppPolicies.UserManagement);
    options.Conventions.AuthorizePage("/Admin/Operations", AppPolicies.SystemOperations);
    options.Conventions.AuthorizeFolder("/Finance", AppPolicies.FinanceLedger);
    options.Conventions.AllowAnonymousToPage("/External/Apply");
    options.Conventions.AllowAnonymousToPage("/Account/Activate");
    options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/Account/ResetPassword");
    options.Conventions.AddPageApplicationModelConvention("/Account/Login", model =>
        model.EndpointMetadata.Add(new EnableRateLimitingAttribute("login")));
    options.Conventions.AddPageApplicationModelConvention("/Account/ForgotPassword", model =>
        model.EndpointMetadata.Add(new EnableRateLimitingAttribute("forgot-password")));
    options.Conventions.AddPageApplicationModelConvention("/Account/Activate", model =>
        model.EndpointMetadata.Add(new EnableRateLimitingAttribute("account-activation")));
    options.Conventions.AddPageApplicationModelConvention("/Account/ResetPassword", model =>
        model.EndpointMetadata.Add(new EnableRateLimitingAttribute("password-reset")));
    options.Conventions.AddPageApplicationModelConvention("/External/Apply", model =>
        model.EndpointMetadata.Add(new EnableRateLimitingAttribute("external-application")));
    if (!useWindowsAuth)
    {
        options.Conventions.AllowAnonymousToPage("/Account/Login");
        options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
        options.Conventions.AllowAnonymousToPage("/Account/Logout");
        options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
    }
});

var canteenOptions = builder.Configuration.GetSection(CanteenBatchingOptions.SectionName).Get<CanteenBatchingOptions>() ?? new CanteenBatchingOptions();
var payrollOptions = builder.Configuration.GetSection(PayrollOptions.SectionName).Get<PayrollOptions>() ?? new PayrollOptions();
var jobSchedulingOptions = builder.Configuration.GetSection(JobSchedulingOptions.SectionName).Get<JobSchedulingOptions>() ?? new JobSchedulingOptions();
var zendeskOptions = builder.Configuration.GetSection(ZendeskSyncOptions.SectionName).Get<ZendeskSyncOptions>() ?? new ZendeskSyncOptions();
var userRegistryOptions = builder.Configuration.GetSection(UserRegistrySyncOptions.SectionName).Get<UserRegistrySyncOptions>() ?? new UserRegistrySyncOptions();
var smtpOptions = builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions();
var batchTimeZone = TimeZoneInfo.FindSystemTimeZoneById(canteenOptions.TimeZoneId);
var payrollTimeZone = TimeZoneInfo.FindSystemTimeZoneById(payrollOptions.TimeZoneId);

builder.Services.AddQuartz(q =>
{
    if (jobSchedulingOptions.UsePersistentStore)
    {
        q.UsePersistentStore(store =>
        {
            store.UseProperties = true;
            store.RetryInterval = TimeSpan.FromSeconds(15);
            store.UseSqlServer(connectionString);
            store.UseSystemTextJsonSerializer();
            if (jobSchedulingOptions.UseClustering)
            {
                store.UseClustering(clustering =>
                {
                    clustering.CheckinInterval = TimeSpan.FromSeconds(20);
                    clustering.CheckinMisfireThreshold = TimeSpan.FromSeconds(60);
                });
            }
        });
    }

    var breakfastJobKey = new JobKey(JobNames.BreakfastCanteenBatch);
    q.AddJob<BreakfastCanteenBatchJob>(opts => opts.WithIdentity(breakfastJobKey));
    q.AddTrigger(opts => opts
        .ForJob(breakfastJobKey)
        .WithIdentity($"{JobNames.BreakfastCanteenBatch}-trigger")
        .WithCronSchedule(canteenOptions.BreakfastCron, cron => cron
            .InTimeZone(batchTimeZone)
            .WithMisfireHandlingInstructionFireAndProceed()));

    var lunchJobKey = new JobKey(JobNames.LunchCanteenBatch);
    q.AddJob<LunchCanteenBatchJob>(opts => opts.WithIdentity(lunchJobKey));
    q.AddTrigger(opts => opts
        .ForJob(lunchJobKey)
        .WithIdentity($"{JobNames.LunchCanteenBatch}-trigger")
        .WithCronSchedule(canteenOptions.LunchCron, cron => cron
            .InTimeZone(batchTimeZone)
            .WithMisfireHandlingInstructionFireAndProceed()));

    var payrollJobKey = new JobKey(JobNames.MonthlyPayroll);
    q.AddJob<MonthlyPayrollJob>(opts => opts.WithIdentity(payrollJobKey));
    q.AddTrigger(opts => opts
        .ForJob(payrollJobKey)
        .WithIdentity($"{JobNames.MonthlyPayroll}-trigger")
        .WithCronSchedule(jobSchedulingOptions.PayrollCron, cron => cron
            .InTimeZone(payrollTimeZone)
            .WithMisfireHandlingInstructionFireAndProceed()));

    var techNewsJobKey = new JobKey(JobNames.TechNewsRefresh);
    q.AddJob<TechNewsRefreshJob>(opts => opts.WithIdentity(techNewsJobKey));
    q.AddTrigger(opts => opts
        .ForJob(techNewsJobKey)
        .WithIdentity($"{JobNames.TechNewsRefresh}-trigger")
        .StartAt(DateTimeOffset.UtcNow.AddSeconds(jobSchedulingOptions.StartupDelaySeconds))
        .WithSimpleSchedule(schedule => schedule
            .WithIntervalInMinutes(jobSchedulingOptions.TechNewsIntervalMinutes)
            .RepeatForever()
            .WithMisfireHandlingInstructionNowWithExistingCount()));

    if (!string.IsNullOrWhiteSpace(zendeskOptions.BaseUrl))
    {
        var zendeskJobKey = new JobKey(JobNames.ZendeskPolicySync);
        q.AddJob<ZendeskPolicySyncJob>(opts => opts.WithIdentity(zendeskJobKey));
        q.AddTrigger(opts => opts
            .ForJob(zendeskJobKey)
            .WithIdentity($"{JobNames.ZendeskPolicySync}-trigger")
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(jobSchedulingOptions.StartupDelaySeconds))
            .WithSimpleSchedule(schedule => schedule
                .WithIntervalInHours(jobSchedulingOptions.ZendeskPolicySyncIntervalHours)
                .RepeatForever()
                .WithMisfireHandlingInstructionNowWithExistingCount()));
    }

    if (userRegistryOptions.Enabled)
    {
        var userSyncJobKey = new JobKey(JobNames.UserRegistrySync);
        q.AddJob<UserRegistrySyncJob>(opts => opts.WithIdentity(userSyncJobKey));
        q.AddTrigger(opts => opts
            .ForJob(userSyncJobKey)
            .WithIdentity($"{JobNames.UserRegistrySync}-trigger")
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(Math.Max(
                userRegistryOptions.InitialDelaySeconds,
                jobSchedulingOptions.StartupDelaySeconds)))
            .WithSimpleSchedule(schedule => schedule
                .WithIntervalInHours(jobSchedulingOptions.UserRegistrySyncIntervalHours)
                .RepeatForever()
                .WithMisfireHandlingInstructionNowWithExistingCount()));
    }

    if (smtpOptions.Enabled)
    {
        var emailJobKey = new JobKey(JobNames.EmailOutboxDispatch);
        q.AddJob<EmailOutboxDispatchJob>(opts => opts.WithIdentity(emailJobKey));
        q.AddTrigger(opts => opts
            .ForJob(emailJobKey)
            .WithIdentity($"{JobNames.EmailOutboxDispatch}-trigger")
            .StartNow()
            .WithSimpleSchedule(schedule => schedule
                .WithIntervalInMinutes(jobSchedulingOptions.EmailDispatchIntervalMinutes)
                .RepeatForever()
                .WithMisfireHandlingInstructionNowWithExistingCount()));
    }
});

builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var app = builder.Build();
var sqlServerSecurity = app.Services.GetRequiredService<IOptions<SqlServerSecurityOptions>>().Value;
var browserSecurity = app.Services.GetRequiredService<IOptions<BrowserSecurityOptions>>().Value;
SqlServerConnectionSecurity.Validate(
    connectionString,
    app.Environment,
    app.Logger,
    sqlServerSecurity.AllowTrustServerCertificateForInternalTest);

if (allowInsecureHttpForInternalTest)
{
    app.Logger.LogWarning(
        "HTTP authentication cookies are temporarily allowed because AuthMode:AllowInsecureHttpForInternalTest is enabled. Use this only for the internal port 8080 test binding and disable it when HTTPS is configured.");
}

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    if (!allowInsecureHttpForInternalTest)
    {
        app.UseHsts();
    }
}

app.UseMiddleware<SecurityHeadersMiddleware>();
if (!allowInsecureHttpForInternalTest)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<AccessDeniedAuditMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
app.MapHealthChecks("/health/database", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database")
}).AllowAnonymous();
app.MapHealthChecks("/health/runtime", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("runtime")
}).AllowAnonymous();

app.MapPost(browserSecurity.ReportPath, CspReportEndpoint.HandleAsync)
    .AllowAnonymous()
    .RequireRateLimiting("csp-report")
    .WithMetadata(new RequestSizeLimitAttribute(CspReportEndpoint.MaxReportBytes));

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
    var testUserSeeder = scope.ServiceProvider.GetRequiredService<ConfiguredTestUserSeeder>();
    await testUserSeeder.SeedAsync();
    await SeedData.InitializeAsync(db);
}

app.Run();
