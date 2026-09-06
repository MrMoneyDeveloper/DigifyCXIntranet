using DigifyCXIntranet.Controllers;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;

namespace DigifyCXIntranet.Tests;

public class SecurityHardeningTests
{
    [Fact]
    public void ZendeskSanitizer_RemovesExecutableMarkup()
    {
        var sanitizer = new ZendeskHtmlSanitizer();

        var result = sanitizer.Sanitize(
            "<p onclick=\"steal()\">Policy</p><script>alert(1)</script><a href=\"javascript:alert(2)\">bad</a>");

        result.Should().Contain("Policy");
        result.ToLowerInvariant().Should().NotContain("script");
        result.ToLowerInvariant().Should().NotContain("onclick");
        result.ToLowerInvariant().Should().NotContain("javascript:");
    }

    [Theory]
    [InlineData("https://example.test/article", "https://example.test/article")]
    [InlineData("http://example.test/article", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("/relative", null)]
    public void ZendeskSanitizer_AllowsOnlyAbsoluteHttpsUrls(string input, string? expected)
    {
        new ZendeskHtmlSanitizer().SanitizeHttpsUrl(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("api/v2/help_center/articles.json", "https://example.zendesk.com/api/v2/help_center/articles.json")]
    [InlineData("https://example.zendesk.com/api/v2/help_center/articles.json?page=2", "https://example.zendesk.com/api/v2/help_center/articles.json?page=2")]
    public void ExternalApiUriPolicy_AllowsOnlySameOriginPagination(string candidate, string expected)
    {
        ExternalApiUriPolicy.ResolveSameOrigin("https://example.zendesk.com", candidate)
            .AbsoluteUri.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://attacker.example/steal")]
    [InlineData("http://example.zendesk.com/api/v2/help_center/articles.json")]
    [InlineData("https://example.zendesk.com.attacker.example/steal")]
    [InlineData("https://user@example.zendesk.com/steal")]
    public void ExternalApiUriPolicy_RejectsCredentialExfiltrationTargets(string candidate)
    {
        var action = () => ExternalApiUriPolicy.ResolveSameOrigin(
            "https://example.zendesk.com",
            candidate);

        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("https://news.example/article", "https://news.example/article")]
    [InlineData("http://news.example/article", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("/relative", null)]
    public void ExternalApiUriPolicy_NormalizesOnlyAbsoluteHttpsLinks(string input, string? expected)
    {
        ExternalApiUriPolicy.NormalizeAbsoluteHttpsUrl(input).Should().Be(expected);
    }

    [Fact]
    public void AuditRedactor_RemovesAssignedSecretsAndBearerTokens()
    {
        var result = AuditRedactor.Redact(
            "password=Welcome123;token=abc123;Authorization=Bearer top.secret.value;status=failed");

        result.Should().Be(
            "password=[redacted];token=[redacted];Authorization=[redacted];status=failed");
        result.Should().NotContain("Welcome123").And.NotContain("abc123").And.NotContain("top.secret.value");
    }

    [Theory]
    [InlineData("shared-secret", "shared-secret", true)]
    [InlineData("shared-secret", "other-secret", false)]
    [InlineData("", "shared-secret", false)]
    [InlineData(null, "shared-secret", false)]
    public void WebhookSecretComparison_HasExpectedResult(string? supplied, string expected, bool matches)
    {
        ZendeskWebhookController.SecretsMatch(supplied, expected).Should().Be(matches);
    }

    [Theory]
    [InlineData("request-123", true)]
    [InlineData("request_123.test", true)]
    [InlineData("request 123", false)]
    [InlineData("request/123", false)]
    [InlineData("", false)]
    public void CorrelationIdValidation_RejectsUnsafeValues(string value, bool valid)
    {
        CorrelationIdMiddleware.IsValid(value).Should().Be(valid);
    }

    [Fact]
    public async Task CorrelationMiddleware_PreservesValidCallerId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "request-123";
        var middleware = new CorrelationIdMiddleware(
            next: _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.TraceIdentifier.Should().Be("request-123");
        context.Items[CorrelationIdMiddleware.HeaderName].Should().Be("request-123");
    }

    [Fact]
    public void IdentifierRateLimiter_RejectsAnIdentifierAtItsConfiguredLimit()
    {
        var options = new RateLimitPoliciesOptions
        {
            Login = new EndpointRateLimitOptions { PermitLimit = 2, WindowMinutes = 5 }
        };
        var limiter = new IdentifierRateLimiter(Microsoft.Extensions.Options.Options.Create(options));

        limiter.TryAcquire("login", "Employee.One").Should().BeTrue();
        limiter.TryAcquire("login", "employee.one").Should().BeTrue();
        limiter.TryAcquire("login", "EMPLOYEE.ONE").Should().BeFalse();
        limiter.TryAcquire("login", "employee.two").Should().BeTrue();
    }

    [Fact]
    public void RuntimeHealth_DegradesAtConfiguredMemoryPressure()
    {
        var snapshot = new RuntimeResourceSnapshot(
            MemoryLoadBytes: 900,
            HighMemoryLoadThresholdBytes: 1000,
            HeapSizeBytes: 600,
            FragmentedBytes: 100,
            TotalCommittedBytes: 700,
            PinnedObjectsCount: 2,
            ThreadPoolThreadCount: 8,
            PendingThreadPoolWorkItems: 0);

        var result = RuntimeResourceHealthCheck.Evaluate(snapshot, new MonitoringOptions
        {
            RuntimeMemoryLoadWarningPercent = 90,
            RuntimeThreadPoolQueueWarningLength = 500
        });

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("GC memory load");
    }

    [Fact]
    public void RuntimeHealth_DegradesForThreadPoolBacklog()
    {
        var snapshot = new RuntimeResourceSnapshot(
            MemoryLoadBytes: 100,
            HighMemoryLoadThresholdBytes: 1000,
            HeapSizeBytes: 80,
            FragmentedBytes: 5,
            TotalCommittedBytes: 90,
            PinnedObjectsCount: 0,
            ThreadPoolThreadCount: 8,
            PendingThreadPoolWorkItems: 500);

        var result = RuntimeResourceHealthCheck.Evaluate(snapshot, new MonitoringOptions
        {
            RuntimeMemoryLoadWarningPercent = 90,
            RuntimeThreadPoolQueueWarningLength = 500
        });

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("thread-pool queue");
    }

    [Fact]
    public void BackupHealth_RequiresLogBackupForFullRecovery()
    {
        var ages = new Dictionary<string, TimeSpan>
        {
            ["D"] = TimeSpan.FromHours(2),
            ["I"] = TimeSpan.FromHours(1)
        };

        var result = BackupHealthCheck.Evaluate(ages, "FULL", new BackupHealthOptions());

        result.IsHealthy.Should().BeFalse();
        result.LogIsRequired.Should().BeTrue();
    }

    [Fact]
    public void BackupHealth_AcceptsFreshBackupChain()
    {
        var ages = new Dictionary<string, TimeSpan>
        {
            ["D"] = TimeSpan.FromHours(2),
            ["I"] = TimeSpan.FromHours(1),
            ["L"] = TimeSpan.FromMinutes(20)
        };

        BackupHealthCheck.Evaluate(ages, "FULL", new BackupHealthOptions())
            .IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void BackupHealth_AcceptsRecentFullBeforeFirstDifferential()
    {
        var ages = new Dictionary<string, TimeSpan>
        {
            ["D"] = TimeSpan.FromMinutes(30)
        };

        BackupHealthCheck.Evaluate(ages, "SIMPLE", new BackupHealthOptions())
            .IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void RegistryUserNormalization_ProducesIdentitySafeUsername()
    {
        var valid = UserRegistrySyncService.TryNormalizeRegistryUser(
            "  Moh\u00E1m   O'Neil  ",
            out var user);

        valid.Should().BeTrue();
        user.Should().NotBeNull();
        user!.DisplayName.Should().Be("Moh\u00E1m O'Neil");
        user.Username.Should().Be("moham.oneil");
        user.NormalizedUsername.Should().Be("MOHAM.ONEIL");
    }

    [Theory]
    [InlineData("Employee Name")]
    [InlineData("   ")]
    [InlineData("\U0001F600")]
    public void RegistryUserNormalization_RejectsNonEmployeeRows(string input)
    {
        UserRegistrySyncService.TryNormalizeRegistryUser(input, out _).Should().BeFalse();
    }

    [Fact]
    public void ContentSecurityPolicy_BlocksInlineScriptsAndFraming()
    {
        var policy = SecurityHeadersMiddleware.BuildContentSecurityPolicy("/security/csp-report");
        var scriptDirective = policy.Split(';', StringSplitOptions.TrimEntries)
            .Single(value => value.StartsWith("script-src", StringComparison.Ordinal));

        scriptDirective.Should().NotContain("'unsafe-inline'");
        policy.Should().Contain("object-src 'none'");
        policy.Should().Contain("frame-ancestors 'none'");
        policy.Should().Contain("report-uri /security/csp-report");
    }

    [Theory]
    [InlineData(StatusCodes.Status403Forbidden, null, true)]
    [InlineData(StatusCodes.Status302Found, "/Account/AccessDenied?ReturnUrl=%2FAdmin", true)]
    [InlineData(StatusCodes.Status302Found, "https://intranet.test/Account/AccessDenied?ReturnUrl=%2FAdmin", true)]
    [InlineData(StatusCodes.Status302Found, "/Account/Login?ReturnUrl=%2FAdmin", false)]
    [InlineData(StatusCodes.Status200OK, null, false)]
    public void AccessDeniedAudit_RecognizesForbiddenResponsesAndCookieRedirects(
        int statusCode,
        string? location,
        bool expected)
    {
        AccessDeniedAuditMiddleware.IsAccessDeniedResponse(statusCode, location)
            .Should().Be(expected);
    }

    [Fact]
    public void CspReport_RemovesQueryStringsAndLogInjectionCharacters()
    {
        using var document = JsonDocument.Parse("""
            {
              "csp-report": {
                "document-uri": "https://intranet.test/Account/Login?token=secret",
                "violated-directive": "script-src\r\ninjected",
                "effective-directive": "script-src-elem",
                "blocked-uri": "https://evil.test/script.js?value=secret",
                "disposition": "report"
              }
            }
            """);

        var report = CspViolationReport.Parse(document.RootElement);

        report.Should().NotBeNull();
        report!.DocumentUri.Should().Be("https://intranet.test/Account/Login");
        report.BlockedUri.Should().Be("https://evil.test/script.js");
        report.ViolatedDirective.Should().Be("script-src  injected");
    }

    [Fact]
    public void DatabasePrincipal_ContainsRevocationClaims()
    {
        var user = new ApplicationUser
        {
            Id = "user-123",
            UserName = "employee.one",
            DisplayName = "Employee One",
            CustomRole = AppRoles.Employee,
            SecurityStamp = "stamp-123"
        };

        var principal = AppAuthenticationClaims.CreateDatabasePrincipal(user, user.UserName);

        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(user.Id);
        principal.FindFirstValue(AppAuthenticationClaims.SecurityStamp).Should().Be(user.SecurityStamp);
        principal.FindFirstValue(AppAuthenticationClaims.AuthenticationSource)
            .Should().Be(AppAuthenticationClaims.IdentityDatabase);
    }

    [Theory]
    [InlineData("stamp-123", false, AppRoles.Employee, "stamp-123", AppRoles.Employee, true)]
    [InlineData("stamp-new", false, AppRoles.Employee, "stamp-old", AppRoles.Employee, false)]
    [InlineData("stamp-123", true, AppRoles.Employee, "stamp-123", AppRoles.Employee, false)]
    [InlineData("stamp-123", false, AppRoles.FinanceAdmin, "stamp-123", AppRoles.Employee, false)]
    public void CookieSessionValidation_RejectsChangedCredentialsOrRoles(
        string currentStamp,
        bool firstTimeLogin,
        string currentRole,
        string cookieStamp,
        string cookieRole,
        bool expected)
    {
        AppCookieAuthenticationEvents.IsCurrentSession(
                currentStamp,
                firstTimeLogin,
                currentRole,
                cookieStamp,
                cookieRole)
            .Should().Be(expected);
    }
}
