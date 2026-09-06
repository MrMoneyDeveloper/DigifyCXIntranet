using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Pages.Finance;
using DigifyCXIntranet.Pages.Policies;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Tests.Pages;

public class ReportQueryTests
{
    [Fact]
    public async Task Compliance_DateFiltersIncludeEntireFinalDayForPageAndExport()
    {
        await using var db = CreatePolicyDb();
        var day = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);
        db.PolicyAcknowledgements.AddRange(
            Acknowledgement(1, day.AddTicks(-1)),
            Acknowledgement(2, day),
            Acknowledgement(3, day.AddDays(1).AddTicks(-1)),
            Acknowledgement(4, day.AddDays(1)));
        await db.SaveChangesAsync();
        var page = CreateCompliancePage(db);
        page.FromUtc = day;
        page.ToUtc = day;
        page.Employee = " employee ";

        await page.OnGetAsync();
        var export = (FileContentResult)await page.OnGetExportCsvAsync();

        page.Items.Select(x => x.Id).Should().Equal(3, 2);
        var csvLines = System.Text.Encoding.UTF8.GetString(export.FileContents)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        csvLines.Should().HaveCount(3);
        csvLines[1].Should().Contain(",3,");
        csvLines[2].Should().Contain(",2,");
    }

    [Fact]
    public async Task Compliance_PaginationUsesUniqueOrderForEqualTimestamps()
    {
        await using var db = CreatePolicyDb();
        var timestamp = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
        db.PolicyAcknowledgements.AddRange(Enumerable.Range(1, 30).Select(id => Acknowledgement(id, timestamp)));
        await db.SaveChangesAsync();
        var page = CreateCompliancePage(db);
        page.PageSize = 25;
        await page.OnGetAsync();
        var firstPageIds = page.Items.Select(x => x.Id).ToArray();
        page.PageNumber = 2;

        await page.OnGetAsync();

        firstPageIds.Should().Equal(Enumerable.Range(6, 25).Reverse());
        page.Items.Select(x => x.Id).Should().Equal(5, 4, 3, 2, 1);
    }

    [Fact]
    public async Task Compliance_OversizedExportReturnsHelpfulErrorWithoutCsv()
    {
        await using var db = CreatePolicyDb();
        db.PolicyAcknowledgements.AddRange(Enumerable.Range(1, 25_001)
            .Select(id => Acknowledgement(id, DateTime.UtcNow)));
        await db.SaveChangesAsync();
        var page = CreateCompliancePage(db);
        page.Employee = "employee";

        var result = await page.OnGetExportCsvAsync();

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().Contain("too large");
        page.Items.Should().HaveCount(100);
    }

    [Fact]
    public async Task Ledger_DateFiltersAndPaginationKeepTotalsForFullFilteredResult()
    {
        await using var db = CreateCanteenDb();
        var day = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);
        db.CanteenOrders.AddRange(Enumerable.Range(1, 30).Select(id => Order(id, day.AddHours(20))));
        db.CanteenOrders.Add(Order(31, day.AddDays(1)));
        await db.SaveChangesAsync();
        var page = CreateLedgerPage(db);
        page.FromUtc = day;
        page.ToUtc = day;
        page.PageNumber = 2;
        page.PageSize = 25;

        await page.OnGetAsync();

        page.DetailRows.Select(x => x.Id).Should().Equal(5, 4, 3, 2, 1);
        page.OrderCount.Should().Be(30);
        page.TotalAmount.Should().Be(300m);
    }

    [Fact]
    public async Task Report_MaximumEndDateDoesNotOverflow()
    {
        await using var policyDb = CreatePolicyDb();
        policyDb.PolicyAcknowledgements.Add(Acknowledgement(1, DateTime.MaxValue));
        await policyDb.SaveChangesAsync();
        var compliance = CreateCompliancePage(policyDb);
        compliance.ToUtc = DateTime.MaxValue.Date;
        await compliance.OnGetAsync();
        compliance.TotalCount.Should().Be(1);

        await using var canteenDb = CreateCanteenDb();
        canteenDb.CanteenOrders.Add(Order(1, DateTime.MaxValue));
        await canteenDb.SaveChangesAsync();
        var ledger = CreateLedgerPage(canteenDb);
        ledger.ToUtc = DateTime.MaxValue.Date;
        await ledger.OnGetAsync();
        ledger.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Ledger_OversizedExportDoesNotBuildWorkbook()
    {
        await using var db = CreateCanteenDb();
        db.CanteenOrders.AddRange(Enumerable.Range(1, 25_001).Select(id => Order(id, DateTime.UtcNow)));
        await db.SaveChangesAsync();
        var page = CreateLedgerPage(db);

        var result = await page.OnGetExportAsync();

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().Contain("too large");
    }

    private static PolicyDbContext CreatePolicyDb() => new(new DbContextOptionsBuilder<PolicyDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CanteenDbContext CreateCanteenDb() => new(new DbContextOptionsBuilder<CanteenDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ComplianceReportModel CreateCompliancePage(PolicyDbContext db) => new(
        db, Microsoft.Extensions.Options.Options.Create(new ZendeskSyncOptions()), new NullAudit())
    {
        PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
    };

    private static CanteenLedgerModel CreateLedgerPage(CanteenDbContext db) => new(
        db, new RejectUnexpectedExport(), new NullFinanceAudit(), new NullAudit())
    {
        PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
    };

    private static PolicyAcknowledgement Acknowledgement(int id, DateTime timestamp) => new()
    {
        Id = id, EmployeeDomainName = "employee", PolicyArticleId = id, PolicyVersion = "1", TimestampUtc = timestamp
    };

    private static CanteenOrder Order(int id, DateTime timestamp) => new()
    {
        Id = id, EmployeeUsername = "employee", ItemSummary = "Lunch", OrderTimeUtc = timestamp, TotalAmount = 10m
    };

    private sealed class NullAudit : IAuditService
    {
        public Task WriteAsync(string actor, string action, string entity, string detail, bool succeeded = true,
            string entityId = "", string errorCode = "", HttpContext? httpContext = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullFinanceAudit : IFinanceAuditService
    {
        public Task WriteAsync(string actor, string action, string entity, string detail,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RejectUnexpectedExport : IFileExportService
    {
        public byte[] BuildCanteenBatchWorkbook(string worksheetName, IEnumerable<CanteenOrder> orders,
            DateTimeOffset generatedAt) => throw new InvalidOperationException("Unexpected export");
        public byte[] BuildPayrollWorkbook(string worksheetName, IEnumerable<PayrollExportRow> rows,
            DateTimeOffset generatedAt) => throw new InvalidOperationException("Unexpected export");
        public byte[] BuildCanteenLedgerWorkbook(IEnumerable<CanteenLedgerSummaryRow> summaryRows,
            IEnumerable<CanteenOrder> detailRows, DateTimeOffset generatedAt) => throw new InvalidOperationException("Unexpected export");
    }
}
