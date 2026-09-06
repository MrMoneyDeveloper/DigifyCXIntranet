using System.Security.Claims;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Tests.Pages;

public class HomePageQueryTests
{
    [Fact]
    public async Task HomePage_MonthlyTotalIncludesOnlyCurrentMonthAndSignedInEmployee()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var canteenDb = new CanteenDbContext(new DbContextOptionsBuilder<CanteenDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var hrDb = new HrDbContext(new DbContextOptionsBuilder<HrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var policyDb = new PolicyDbContext(new DbContextOptionsBuilder<PolicyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        canteenDb.CanteenOrders.AddRange(
            Order("employee", monthStart.AddTicks(-1), 100),
            Order("employee", monthStart, 10),
            Order("employee", nextMonth.AddTicks(-1), 20),
            Order("employee", nextMonth, 200),
            Order("other", monthStart, 300));
        await canteenDb.SaveChangesAsync();
        var page = new DigifyCXIntranet.Pages.IndexModel(db, canteenDb, hrDb, policyDb, new EmployeeAccess(),
            Microsoft.Extensions.Options.Options.Create(new HomePageOptions()),
            Microsoft.Extensions.Options.Options.Create(new ZendeskSyncOptions()))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, @"DOMAIN\employee")], "test"))
                }
            }
        };

        await page.OnGetAsync();

        page.CurrentMonthCanteenTotal.Should().Be(30m);
    }

    private static CanteenOrder Order(string employee, DateTime timestamp, decimal amount) => new()
    {
        EmployeeUsername = employee, ItemSummary = "Lunch", OrderTimeUtc = timestamp, TotalAmount = amount
    };

    private sealed class EmployeeAccess : IAdminAccessService
    {
        public bool IsAdmin(ClaimsPrincipal user) => false;
        public string GetPrimaryRole(ClaimsPrincipal user) => "Employee";
    }
}
