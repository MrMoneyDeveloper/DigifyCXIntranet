using DigifyCXIntranet.Controllers;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Pages.Account;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigifyCXIntranet.Tests;

public sealed class AccountActivationTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    [InlineData(16, false)]
    public void ActivationGrant_HasShortLifetimeAndRejectsFutureDates(int ageMinutes, bool expected)
    {
        var now = new DateTimeOffset(2026, 9, 6, 8, 0, 0, TimeSpan.Zero);
        var tempData = CreateTempData(new DefaultHttpContext());
        AccountActivationSession.Issue(tempData, new ApplicationUser { UserName = "employee.one" },
            "protected-reset-token", "127.0.0.1", now.AddMinutes(-ageMinutes));

        (AccountActivationSession.Read(tempData, now) is not null).Should().Be(expected);
    }

    [Fact]
    public void ActivationGrant_RejectsLegacyCookieWithoutVerificationToken()
    {
        var tempData = CreateTempData(new DefaultHttpContext());
        tempData["ActivationUsername"] = "employee.one";
        tempData["ActivationUserId"] = "employee-id";

        AccountActivationSession.Read(tempData, DateTimeOffset.UtcNow).Should().BeNull();
    }

    [Fact]
    public async Task ActivationAndPasswordSetup_CompleteWithoutChangingTheBusinessFlow()
    {
        using var fixture = new Fixture();
        var user = await fixture.CreateUserAsync("employee.one", "Employee One");
        var activation = fixture.CreateActivationPage("Employee One");

        (await activation.OnPostAsync()).Should().BeOfType<RedirectToPageResult>()
            .Which.PageName.Should().Be("/Account/ResetPassword");
        var grant = AccountActivationSession.Read(fixture.TempData, DateTimeOffset.UtcNow);
        grant.Should().NotBeNull();
        grant!.UserId.Should().Be(user.Id);
        (await fixture.Users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose, grant.ResetToken)).Should().BeTrue();

        var reset = fixture.CreateResetPage();
        (await reset.OnPostAsync()).Should().BeOfType<RedirectToPageResult>()
            .Which.PageName.Should().Be("/Index");
        user.IsFirstTimeLogin.Should().BeFalse();
        (await fixture.Users.CheckPasswordAsync(user, Fixture.NewPassword)).Should().BeTrue();
        fixture.TempData.Should().NotContainKey("ActivationResetToken");
        (await fixture.Db.AccountActivationLogs.CountAsync()).Should().Be(1);
        (await fixture.Users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose, grant.ResetToken)).Should().BeFalse();
    }

    [Fact]
    public async Task PasswordSetup_RejectsOldGrantAfterAccountIsResetAgain()
    {
        using var fixture = new Fixture();
        var user = await fixture.CreateUserAsync("employee.one", "Employee One");
        await fixture.IssueGrantAsync(user);
        (await fixture.Users.UpdateSecurityStampAsync(user)).Succeeded.Should().BeTrue();
        var currentStamp = user.SecurityStamp;

        var reset = fixture.CreateResetPage();
        (await reset.OnPostAsync()).Should().BeOfType<PageResult>();

        reset.ErrorMessage.Should().Contain("no longer valid");
        user.IsFirstTimeLogin.Should().BeTrue();
        user.PasswordHash.Should().BeNull();
        user.SecurityStamp.Should().Be(currentStamp);
        fixture.Audit.ErrorCodes.Should().Contain("InvalidActivationToken");
        fixture.TempData.Should().NotContainKey("ActivationResetToken");
        (await fixture.Db.AccountActivationLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task PasswordSetup_CannotApplyAnotherUsersTokenToAnAccount()
    {
        using var fixture = new Fixture();
        var first = await fixture.CreateUserAsync("employee.one", "Employee One");
        var second = await fixture.CreateUserAsync("employee.two", "Employee Two");
        await fixture.IssueGrantAsync(first);
        fixture.TempData["ActivationUserId"] = second.Id;
        fixture.TempData["ActivationUsername"] = second.UserName;

        (await fixture.CreateResetPage().OnPostAsync()).Should().BeOfType<PageResult>();

        first.PasswordHash.Should().BeNull();
        second.PasswordHash.Should().BeNull();
        second.IsFirstTimeLogin.Should().BeTrue();
        fixture.TempData.Should().NotContainKey("ActivationResetToken");
    }

    [Fact]
    public async Task PasswordSetup_RejectsGrantIfUsernameHasBeenReassigned()
    {
        using var fixture = new Fixture();
        var first = await fixture.CreateUserAsync("employee.one", "Employee One");
        await fixture.IssueGrantAsync(first);
        (await fixture.Users.DeleteAsync(first)).Succeeded.Should().BeTrue();
        var replacement = await fixture.CreateUserAsync("employee.one", "New Employee");

        var reset = fixture.CreateResetPage();
        (await reset.OnPostAsync()).Should().BeOfType<PageResult>();

        reset.ErrorMessage.Should().Contain("no longer valid");
        replacement.PasswordHash.Should().BeNull();
        replacement.IsFirstTimeLogin.Should().BeTrue();
        fixture.TempData.Should().NotContainKey("ActivationResetToken");
    }

    [Fact]
    public async Task PasswordSetup_PreservesGrantAndUsernameWhenPasswordNeedsCorrection()
    {
        using var fixture = new Fixture();
        var user = await fixture.CreateUserAsync("employee.one", "Employee One");
        await fixture.IssueGrantAsync(user);
        var reset = fixture.CreateResetPage();
        reset.Input.Password = "a";
        reset.Input.ConfirmPassword = "a";

        (await reset.OnPostAsync()).Should().BeOfType<PageResult>();

        reset.ActivationUsername.Should().Be(user.UserName);
        AccountActivationSession.Read(fixture.TempData, DateTimeOffset.UtcNow).Should().NotBeNull();
        user.PasswordHash.Should().BeNull();
        user.IsFirstTimeLogin.Should().BeTrue();
        (await fixture.CreateResetPage().OnPostAsync()).Should().BeOfType<RedirectToPageResult>();
    }

    [Fact]
    public async Task PasswordSetup_RejectsExpiredGrantAndClearsIt()
    {
        using var fixture = new Fixture();
        var user = await fixture.CreateUserAsync("employee.one", "Employee One");
        await fixture.IssueGrantAsync(user, DateTimeOffset.UtcNow.AddMinutes(-16));
        var reset = fixture.CreateResetPage();

        (await reset.OnPostAsync()).Should().BeOfType<PageResult>();

        reset.ErrorMessage.Should().Contain("expired");
        user.PasswordHash.Should().BeNull();
        fixture.TempData.Should().NotContainKey("ActivationResetToken");
    }

    [Fact]
    public async Task PasswordSetup_FailedIdentityValidationCannotBePersistedByLaterAuditSave()
    {
        using var fixture = new Fixture();
        var user = await fixture.CreateUserAsync("employee.one", "Employee One");
        var originalStamp = user.SecurityStamp;
        await fixture.IssueGrantAsync(user);
        fixture.Users.UserValidators.Add(new RejectActivationValidator());

        (await fixture.CreateResetPage().OnPostAsync()).Should().BeOfType<PageResult>();
        await fixture.Db.SaveChangesAsync();

        var persisted = await fixture.Db.Users.AsNoTracking().SingleAsync(candidate => candidate.Id == user.Id);
        persisted.PasswordHash.Should().BeNull();
        persisted.IsFirstTimeLogin.Should().BeTrue();
        persisted.SecurityStamp.Should().Be(originalStamp);
    }

    [Fact]
    public async Task Activation_RejectsAmbiguousDisplayNames()
    {
        using var fixture = new Fixture();
        await fixture.CreateUserAsync("employee.one", "Shared Name");
        await fixture.CreateUserAsync("employee.two", "Shared Name");
        var activation = fixture.CreateActivationPage("Shared Name");

        (await activation.OnPostAsync()).Should().BeOfType<PageResult>();

        activation.ErrorMessage.Should().Contain("could not verify");
        fixture.TempData.Should().NotContainKey("ActivationResetToken");
    }

    [Fact]
    public async Task PasswordResetWebhook_RejectsAmbiguousNamesWithoutChangingEitherAccount()
    {
        using var fixture = new Fixture();
        var first = await fixture.CreateUserAsync("employee.one", "Shared Name");
        var second = await fixture.CreateUserAsync("employee.two", "Shared Name");
        var firstStamp = first.SecurityStamp;
        var secondStamp = second.SecurityStamp;
        var controller = new ZendeskWebhookController(fixture.Db,
            Microsoft.Extensions.Options.Options.Create(new ZendeskWebhookOptions { Secret = "test-webhook-secret" }),
            fixture.Audit, NullLogger<ZendeskWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = fixture.HttpContext }
        };
        fixture.HttpContext.Request.Headers["DigifyCX_Reset_Password_Secret"] = "test-webhook-secret";

        var result = await controller.ResetPassword(new ResetPasswordRequest
        {
            DisplayName = "Shared Name", TicketId = "ticket-123"
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        fixture.Audit.ErrorCodes.Should().Contain("AmbiguousUser");
        first.SecurityStamp.Should().Be(firstStamp);
        second.SecurityStamp.Should().Be(secondStamp);
        first.PasswordHash.Should().BeNull();
        second.PasswordHash.Should().BeNull();
    }

    private static ITempDataDictionary CreateTempData(HttpContext context) =>
        new TempDataDictionary(context, new TestTempDataProvider());

    private sealed class Fixture : IDisposable
    {
        internal const string NewPassword = "Employee-New-Password1!";
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        internal ApplicationDbContext Db { get; }
        internal UserManager<ApplicationUser> Users { get; }
        internal DefaultHttpContext HttpContext { get; }
        internal ITempDataDictionary TempData { get; }
        internal TestAuditService Audit { get; } = new();

        internal Fixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            Db = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            HttpContext = new DefaultHttpContext { RequestServices = _scope.ServiceProvider };
            TempData = CreateTempData(HttpContext);
        }

        internal async Task<ApplicationUser> CreateUserAsync(string username, string displayName)
        {
            var user = new ApplicationUser { UserName = username, DisplayName = displayName };
            (await Users.CreateAsync(user)).Succeeded.Should().BeTrue();
            return user;
        }

        internal async Task IssueGrantAsync(ApplicationUser user, DateTimeOffset? now = null) =>
            AccountActivationSession.Issue(TempData, user,
                await Users.GeneratePasswordResetTokenAsync(user), "127.0.0.1", now ?? DateTimeOffset.UtcNow);

        internal ActivateModel CreateActivationPage(string fullName) => new(Db,
            Microsoft.Extensions.Options.Options.Create(new ActivationOptions { DefaultPassword = "test-activation-password" }),
            Audit, new TestRateLimiter(), Users)
        {
            PageContext = new PageContext { HttpContext = HttpContext },
            TempData = TempData,
            Input = new ActivateModel.InputModel
            {
                FullName = fullName, DefaultPassword = "test-activation-password"
            }
        };

        internal ResetPasswordModel CreateResetPage() => new(Db, Users, Audit,
            NullLogger<ResetPasswordModel>.Instance, new TestRateLimiter())
        {
            PageContext = new PageContext { HttpContext = HttpContext },
            TempData = TempData,
            Input = new ResetPasswordModel.InputModel { Password = NewPassword, ConfirmPassword = NewPassword }
        };

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestRateLimiter : IIdentifierRateLimiter
    {
        public bool TryAcquire(string policyName, string? identifier) => true;
    }

    private sealed class RejectActivationValidator : IUserValidator<ApplicationUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "AccountValidationFailed", Description = "Account validation failed."
            }));
    }

    private sealed class TestAuditService : IAuditService
    {
        internal List<string> ErrorCodes { get; } = new();
        public Task WriteAsync(string actor, string action, string entity, string detail,
            bool succeeded = true, string entityId = "", string errorCode = "",
            HttpContext? httpContext = null, CancellationToken cancellationToken = default)
        {
            ErrorCodes.Add(errorCode);
            return Task.CompletedTask;
        }
    }
}
