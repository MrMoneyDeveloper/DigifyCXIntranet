using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Finance;

public class CanteenLedgerModel : PageModel
{
    private const int ExportRowLimit = 25_000;
    private readonly CanteenDbContext _db;
    private readonly IFileExportService _fileExportService;
    private readonly IFinanceAuditService _auditService;
    private readonly IAuditService _generalAuditService;

    public CanteenLedgerModel(
        CanteenDbContext db,
        IFileExportService fileExportService,
        IFinanceAuditService auditService,
        IAuditService generalAuditService)
    {
        _db = db;
        _fileExportService = fileExportService;
        _auditService = auditService;
        _generalAuditService = generalAuditService;
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
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 100;
    public int TotalCount { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;
        NormalizeDates();
        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 25, 200);
        var query = BuildFilteredQuery();
        TotalCount = await query.CountAsync(cancellationToken);
        PageNumber = Math.Min(PageNumber, Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize)));
        DetailRows = await query
            .OrderBy(x => x.EmployeeUsername)
            .ThenByDescending(x => x.OrderTimeUtc)
            .ThenByDescending(x => x.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        SummaryRows = await query
            .GroupBy(x => x.EmployeeUsername)
            .Select(group => new CanteenLedgerSummaryRow
            {
                EmployeeUsername = group.Key,
                OrderCount = group.Count(),
                TotalAmount = group.Sum(order => order.TotalAmount)
            })
            .OrderByDescending(row => row.TotalAmount)
            .ThenBy(row => row.EmployeeUsername)
            .ToListAsync(cancellationToken);
        TotalAmount = SummaryRows.Sum(x => x.TotalAmount);
        OrderCount = TotalCount;
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        NormalizeDates();
        var query = BuildFilteredQuery();
        // Bound the materialized result itself so inserts during export cannot bypass the limit.
        var detailRows = await query
            .OrderBy(x => x.EmployeeUsername)
            .ThenByDescending(x => x.OrderTimeUtc)
            .ThenByDescending(x => x.Id)
            .Take(ExportRowLimit + 1)
            .ToListAsync(HttpContext.RequestAborted);
        if (detailRows.Count > ExportRowLimit)
        {
            await _generalAuditService.WriteAsync(
                UserNameHelper.GetShortName(User),
                "ExportRejected",
                "CanteenLedger",
                $"rows>{ExportRowLimit}",
                succeeded: false,
                errorCode: "ExportTooLarge",
                httpContext: HttpContext);
            ErrorMessage = "The export is too large. Narrow the employee or date filters and try again.";
            await OnGetAsync();
            return Page();
        }

        var summaryRows = BuildSummaryRows(detailRows);
        var bytes = _fileExportService.BuildCanteenLedgerWorkbook(
            summaryRows,
            detailRows,
            DateTimeOffset.UtcNow);

        var fileName = $"canteen_ledger_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Export", "CanteenLedger", $"employee={Employee};from={FromUtc:yyyy-MM-dd};to={ToUtc:yyyy-MM-dd};rows={detailRows.Count};file={fileName}");
        await _generalAuditService.WriteAsync(
            UserNameHelper.GetShortName(User),
            "Export",
            "CanteenLedger",
            $"employee-filter={(!string.IsNullOrWhiteSpace(Employee))};from={FromUtc:yyyy-MM-dd};to={ToUtc:yyyy-MM-dd};rows={detailRows.Count};file={fileName}",
            httpContext: HttpContext);
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

        if (ToUtc.HasValue && ToUtc.Value.Date < DateTime.MaxValue.Date)
        {
            var endExclusive = ToUtc.Value.Date.AddDays(1);
            query = query.Where(x => x.OrderTimeUtc < endExclusive);
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
