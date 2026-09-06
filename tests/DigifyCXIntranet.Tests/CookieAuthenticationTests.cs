using System.Security.Claims;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigifyCXIntranet.Tests;

public sealed class CookieAuthenticationTests
{
    [Theory]
    [InlineData(null, "Production", false)]
    [InlineData(null, "Development", false)]
    [InlineData("unknown", "Production", false)]
    [InlineData(AppAuthenticationClaims.DevelopmentUser, "Production", false)]
    [InlineData(AppAuthenticationClaims.DevelopmentUser, "Staging", false)]
    [InlineData(AppAuthenticationClaims.DevelopmentUser, "Development", true)]
    public async Task CookieValidation_RejectsUnknownAndNonDevelopmentDemoSessions(
        string? source, string environmentName, bool accepted)
    {
        using var fixture = new Fixture(environmentName);
        var claims = new List<Claim> { new(ClaimTypes.Name, "employee.one"), new(ClaimTypes.Role, AppRoles.Employee) };
        if (source is not null)
        {
            claims.Add(new Claim(AppAuthenticationClaims.AuthenticationSource, source));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        var context = fixture.CreateContext(principal);

        await fixture.Events.ValidatePrincipal(context);

        (context.Principal is not null).Should().Be(accepted);
        if (!accepted)
        {
            fixture.HttpContext.Response.Headers.SetCookie.ToString().Should().Contain("expires=");
        }
    }

    [Fact]
    public async Task CookieValidation_AcceptsCurrentDatabaseAccount()
    {
        using var fixture = new Fixture("Production");
        var user = new ApplicationUser
        {
            UserName = "employee.one", SecurityStamp = "stamp-current", IsFirstTimeLogin = false
        };
        fixture.Db.Users.Add(user);
        await fixture.Db.SaveChangesAsync();
        var context = fixture.CreateContext(AppAuthenticationClaims.CreateDatabasePrincipal(user, user.UserName));

        await fixture.Events.ValidatePrincipal(context);

        context.Principal.Should().NotBeNull();
    }

    [Fact]
    public async Task CookieValidation_RejectsDeletedAccount()
    {
        using var fixture = new Fixture("Production");
        var user = new ApplicationUser
        {
            UserName = "employee.one", SecurityStamp = "stamp-current", IsFirstTimeLogin = false
        };
        var context = fixture.CreateContext(AppAuthenticationClaims.CreateDatabasePrincipal(user, user.UserName));

        await fixture.Events.ValidatePrincipal(context);

        context.Principal.Should().BeNull();
    }

    [Fact]
    public async Task CookieValidation_RejectsDatabaseCookieWithoutRevocationClaims()
    {
        using var fixture = new Fixture("Production");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "employee.one"),
            new Claim(AppAuthenticationClaims.AuthenticationSource, AppAuthenticationClaims.IdentityDatabase)
        }, CookieAuthenticationDefaults.AuthenticationScheme));
        var context = fixture.CreateContext(principal);

        await fixture.Events.ValidatePrincipal(context);

        context.Principal.Should().BeNull();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 100 });
        internal ApplicationDbContext Db { get; }
        internal DefaultHttpContext HttpContext { get; }
        internal AppCookieAuthenticationEvents Events { get; }

        internal Fixture(string environmentName)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
            _provider = services.BuildServiceProvider();
            HttpContext = new DefaultHttpContext { RequestServices = _provider };
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Events = new AppCookieAuthenticationEvents(Db, _cache,
                NullLogger<AppCookieAuthenticationEvents>.Instance,
                new TestEnvironment { EnvironmentName = environmentName });
        }

        internal CookieValidatePrincipalContext CreateContext(ClaimsPrincipal principal) => new(HttpContext,
            new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme));

        public void Dispose()
        {
            Db.Dispose();
            _cache.Dispose();
            _provider.Dispose();
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "DigifyCXIntranet.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
