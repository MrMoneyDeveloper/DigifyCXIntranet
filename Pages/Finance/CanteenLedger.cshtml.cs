using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Finance;

public class CanteenLedgerModel : PageModel
{
    private readonly CanteenDbContext _db;
    private readonly IFileExportService _fileExportService;
    private readonly IFinanceAuditService _auditService;

    public CanteenLedgerModel(CanteenDbContext db, IFileExportService fileExportService, IFinanceAuditService auditService)
    {
        _db = db;
        _fileExportService = fileExportService;
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public string Employee { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public DateTime? FromUtc { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToUtc { get; set; }

    public List<CanteenLedgerSummaryRow> SummaryRows { get; private set; } = new();
    public List<CanteenOrder> DetailRows { get; private set; } = new();
    public decimal TotalAmount { get; private set; }
    public int OrderCount { get; private set; }

    public async Task OnGetAsync()
    {
        NormalizeDates();
        DetailRows = await BuildFilteredQuery()
            .OrderBy(x => x.EmployeeUsername)
            .ThenByDescending(x => x.OrderTimeUtc)
            .Take(1000)
            .ToListAsync();

        SummaryRows = BuildSummaryRows(DetailRows);
        TotalAmount = SummaryRows.Sum(x => x.TotalAmount);
        OrderCount = SummaryRows.Sum(x => x.OrderCount);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        NormalizeDates();
        var detailRows = await BuildFilteredQuery()
            .OrderBy(x => x.EmployeeUsername)
            .ThenByDescending(x => x.OrderTimeUtc)
            .ToListAsync();

        var summaryRows = BuildSummaryRows(detailRows);
        var bytes = _fileExportService.BuildCanteenLedgerWorkbook(
            summaryRows,
            detailRows,
            DateTimeOffset.UtcNow);

        var fileName = $"canteen_ledger_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Export", "CanteenLedger", $"employee={Employee};from={FromUtc:yyyy-MM-dd};to={ToUtc:yyyy-MM-dd};rows={detailRows.Count};file={fileName}");
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private IQueryable<CanteenOrder> BuildFilteredQuery()
    {
        var query = _db.CanteenOrders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Employee))
        {
            query = query.Where(x => x.EmployeeUsername.Contains(Employee.Trim()));
        }

        if (FromUtc.HasValue)
        {
            query = query.Where(x => x.OrderTimeUtc >= FromUtc.Value);
        }

        if (ToUtc.HasValue)
        {
            query = query.Where(x => x.OrderTimeUtc < ToUtc.Value.AddDays(1));
        }

        return query;
    }

    private void NormalizeDates()
    {
        if (FromUtc.HasValue)
        {
            FromUtc = DateTime.SpecifyKind(FromUtc.Value.Date, DateTimeKind.Utc);
        }

        if (ToUtc.HasValue)
        {
            ToUtc = DateTime.SpecifyKind(ToUtc.Value.Date, DateTimeKind.Utc);
        }

        if (!FromUtc.HasValue && !ToUtc.HasValue)
        {
            var today = DateTime.UtcNow.Date;
            FromUtc = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            ToUtc = today;
        }
    }

    private static List<CanteenLedgerSummaryRow> BuildSummaryRows(IEnumerable<CanteenOrder> detailRows)
    {
        return detailRows
            .GroupBy(x => x.EmployeeUsername)
            .Select(x => new CanteenLedgerSummaryRow
            {
                EmployeeUsername = x.Key,
                OrderCount = x.Count(),
                TotalAmount = x.Sum(v => v.TotalAmount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.EmployeeUsername)
            .ToList();
    }
}
