using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;

namespace DigifyCXIntranet.Pages.Policies;

[Authorize(Policy = AppPolicies.HrOperations)]
public class ComplianceReportModel : PageModel
{
    private const int ExportRowLimit = 25_000;
    private readonly PolicyDbContext _db;
    private readonly ZendeskSyncOptions _zendeskSyncOptions;
    private readonly IAuditService _auditService;

    public ComplianceReportModel(
        PolicyDbContext db,
        IOptions<ZendeskSyncOptions> zendeskSyncOptions,
        IAuditService auditService)
    {
        _db = db;
        _zendeskSyncOptions = zendeskSyncOptions.Value;
        _auditService = auditService;
    }

    [BindProperty(SupportsGet = true)]
    public string Employee { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public long? PolicyArticleId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromUtc { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToUtc { get; set; }

    public List<PolicyAcknowledgement> Items { get; private set; } = new();
    public List<SelectListItem> PolicyOptions { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 100;
    public int TotalCount { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        NormalizeFilters();
        var cancellationToken = HttpContext.RequestAborted;
        PolicyOptions = await _db.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .InAllowedZendeskSections(_zendeskSyncOptions)
            .OrderBy(x => x.Title)
            .Select(x => new SelectListItem
            {
                Text = x.Title,
                Value = x.ZendeskArticleId.ToString()
            })
            .ToListAsync(cancellationToken);

        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 25, 200);
        var query = BuildFilteredQuery();
        TotalCount = await query.CountAsync(cancellationToken);
        PageNumber = Math.Min(PageNumber, Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize)));
        Items = await query
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        NormalizeFilters();
        if (!HasExplicitFilter())
        {
            await _auditService.WriteAsync(
                UserNameHelper.GetShortName(User),
                "ExportRejected",
                "PolicyCompliance",
                "reason=filters-required",
                succeeded: false,
                errorCode: "FiltersRequired",
                httpContext: HttpContext);
            ErrorMessage = "Select at least one policy, employee, or date filter before exporting.";
            await OnGetAsync();
            return Page();
        }

        var query = BuildFilteredQuery();
        // Bound the materialized result itself so inserts during export cannot bypass the limit.
        var rows = await query
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id)
            .Take(ExportRowLimit + 1)
            .ToListAsync(HttpContext.RequestAborted);
        if (rows.Count > ExportRowLimit)
        {
            await _auditService.WriteAsync(
                UserNameHelper.GetShortName(User),
                "ExportRejected",
                "PolicyCompliance",
                $"rows>{ExportRowLimit}",
                succeeded: false,
                errorCode: "ExportTooLarge",
                httpContext: HttpContext);
            ErrorMessage = "The export is too large. Narrow the filters and try again.";
            await OnGetAsync();
            return Page();
        }

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeDomainName,PolicyArticleId,PolicyVersion,TimestampUtc");
        foreach (var row in rows)
        {
            sb.AppendLine($"{Escape(row.EmployeeDomainName)},{row.PolicyArticleId},{Escape(row.PolicyVersion)},{row.TimestampUtc:O}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"policy_compliance_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        await _auditService.WriteAsync(
            UserNameHelper.GetShortName(User),
            "Export",
            "PolicyCompliance",
            $"employee-filter={(!string.IsNullOrWhiteSpace(Employee))};policy-filter={PolicyArticleId.HasValue};from={FromUtc:yyyy-MM-dd};to={ToUtc:yyyy-MM-dd};rows={rows.Count};file={fileName}",
            httpContext: HttpContext);
        return File(bytes, "text/csv", fileName);
    }

    private bool HasExplicitFilter() =>
        !string.IsNullOrWhiteSpace(Employee) ||
        PolicyArticleId.HasValue ||
        FromUtc.HasValue ||
        ToUtc.HasValue;

    private IQueryable<PolicyAcknowledgement> BuildFilteredQuery()
    {
        var query = _db.PolicyAcknowledgements.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Employee))
        {
            query = query.Where(x => x.EmployeeDomainName.Contains(Employee));
        }

        if (PolicyArticleId.HasValue)
        {
            query = query.Where(x => x.PolicyArticleId == PolicyArticleId.Value);
        }

        if (FromUtc.HasValue)
        {
            query = query.Where(x => x.TimestampUtc >= FromUtc.Value);
        }

        if (ToUtc.HasValue && ToUtc.Value.Date < DateTime.MaxValue.Date)
        {
            var endExclusive = ToUtc.Value.Date.AddDays(1);
            query = query.Where(x => x.TimestampUtc < endExclusive);
        }

        return query;
    }

    private void NormalizeFilters()
    {
        Employee = Employee?.Trim() ?? string.Empty;
        if (FromUtc.HasValue)
        {
            FromUtc = DateTime.SpecifyKind(FromUtc.Value.Date, DateTimeKind.Utc);
        }

        if (ToUtc.HasValue)
        {
            ToUtc = DateTime.SpecifyKind(ToUtc.Value.Date, DateTimeKind.Utc);
        }
    }

    private static string Escape(string value)
    {
        var safe = value;
        if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            safe = $"'{safe}";
        }

        safe = safe.Replace("\"", "\"\"");
        return $"\"{safe}\"";
    }
}
