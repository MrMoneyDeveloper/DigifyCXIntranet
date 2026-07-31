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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Quartz;
using Microsoft.AspNetCore.RateLimiting;
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
builder.Services.AddOptions<RateLimitPoliciesOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitPoliciesOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<BackupHealthOptions>()
    .Bind(builder.Configuration.GetSection(BackupHealthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<JobSchedulingOptions>()
    .Bind(builder.Configuration.GetSection(JobSchedulingOptions.SectionName))
    .ValidateDataAnnotations()
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
        var key = context.Connection.RemoteIpAddress?.ToString()
            ?? context.User.Identity?.Name
            ?? "anonymous";

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

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<DatabaseHealthCheck>("sqlserver", tags: new[] { "ready", "database" })
    .AddCheck<BackupHealthCheck>("backup", tags: new[] { "ready", "database" })
    .AddCheck<CriticalJobHealthCheck>("critical-jobs", tags: new[] { "ready" });

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
    }
});

var canteenOptions = builder.Configuration.GetSection(CanteenBatchingOptions.SectionName).Get<CanteenBatchingOptions>() ?? new CanteenBatchingOptions();
var jobOptions = builder.Configuration.GetSection(JobSchedulingOptions.SectionName).Get<JobSchedulingOptions>() ?? new JobSchedulingOptions();
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
    if (jobOptions.UsePersistentStore)
    {
        q.UsePersistentStore(store =>
        {
            store.UseProperties = true;
            store.UseSqlServer(connectionString);
            if (jobOptions.UseClustering)
            {
                store.UseClustering();
            }
        });
    }

    var breakfastJobKey = new JobKey(JobNames.BreakfastCanteenBatch);
    q.AddJob<BreakfastCanteenBatchJob>(opts => opts.WithIdentity(breakfastJobKey));
    q.AddTrigger(opts => opts
        .ForJob(breakfastJobKey)
        .WithIdentity($"{JobNames.BreakfastCanteenBatch}-trigger")
        .StartAt(DateBuilder.FutureDate(jobOptions.StartupDelaySeconds, IntervalUnit.Second))
        .WithCronSchedule(canteenOptions.BreakfastCron, cron => cron.InTimeZone(batchTimeZone).WithMisfireHandlingInstructionFireAndProceed()));

    var lunchJobKey = new JobKey(JobNames.LunchCanteenBatch);
    q.AddJob<LunchCanteenBatchJob>(opts => opts.WithIdentity(lunchJobKey));
    q.AddTrigger(opts => opts
        .ForJob(lunchJobKey)
        .WithIdentity($"{JobNames.LunchCanteenBatch}-trigger")
        .StartAt(DateBuilder.FutureDate(jobOptions.StartupDelaySeconds, IntervalUnit.Second))
        .WithCronSchedule(canteenOptions.LunchCron, cron => cron.InTimeZone(batchTimeZone).WithMisfireHandlingInstructionFireAndProceed()));

    AddSimpleIntervalJob<TechNewsRefreshJob>(q, JobNames.TechNewsRefresh, TimeSpan.FromMinutes(jobOptions.TechNewsIntervalMinutes), jobOptions.StartupDelaySeconds);
    AddSimpleIntervalJob<ZendeskPolicySyncJob>(q, JobNames.ZendeskPolicySync, TimeSpan.FromHours(jobOptions.ZendeskPolicySyncIntervalHours), jobOptions.StartupDelaySeconds + 30);
    AddSimpleIntervalJob<UserRegistrySyncJob>(q, JobNames.UserRegistrySync, TimeSpan.FromHours(jobOptions.UserRegistrySyncIntervalHours), jobOptions.StartupDelaySeconds + 60);
    AddSimpleIntervalJob<EmailOutboxDispatchJob>(q, JobNames.EmailOutboxDispatch, TimeSpan.FromMinutes(jobOptions.EmailDispatchIntervalMinutes), jobOptions.StartupDelaySeconds);

    var payrollJobKey = new JobKey(JobNames.MonthlyPayroll);
    q.AddJob<MonthlyPayrollJob>(opts => opts.WithIdentity(payrollJobKey));
    q.AddTrigger(opts => opts
        .ForJob(payrollJobKey)
        .WithIdentity($"{JobNames.MonthlyPayroll}-trigger")
        .StartAt(DateBuilder.FutureDate(jobOptions.StartupDelaySeconds, IntervalUnit.Second))
        .WithCronSchedule(jobOptions.PayrollCron, cron => cron.InTimeZone(batchTimeZone).WithMisfireHandlingInstructionFireAndProceed()));
});

static void AddSimpleIntervalJob<TJob>(IServiceCollectionQuartzConfigurator q, string jobName, TimeSpan interval, int startupDelaySeconds)
    where TJob : IJob
{
    var jobKey = new JobKey(jobName);
    q.AddJob<TJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{jobName}-trigger")
        .StartAt(DateBuilder.FutureDate(startupDelaySeconds, IntervalUnit.Second))
        .WithSimpleSchedule(schedule => schedule
            .WithInterval(interval)
            .RepeatForever()
            .WithMisfireHandlingInstructionNextWithRemainingCount()));
}

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

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/database", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database")
});

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
