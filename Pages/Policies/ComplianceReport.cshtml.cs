using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DigifyCXIntranet.Pages.Policies;

[Authorize(Policy = AppPolicies.HrOperations)]
public class ComplianceReportModel : PageModel
{
    private readonly PolicyDbContext _db;

    public ComplianceReportModel(PolicyDbContext db)
    {
        _db = db;
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

    public async Task OnGetAsync()
    {
        PolicyOptions = await _db.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.Title)
            .Select(x => new SelectListItem
            {
                Text = x.Title,
                Value = x.ZendeskArticleId.ToString()
            })
            .ToListAsync();

        Items = await BuildFilteredQuery()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(500)
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        var rows = await BuildFilteredQuery()
            .OrderByDescending(x => x.TimestampUtc)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeDomainName,PolicyArticleId,PolicyVersion,TimestampUtc");
        foreach (var row in rows)
        {
            sb.AppendLine($"{Escape(row.EmployeeDomainName)},{row.PolicyArticleId},{Escape(row.PolicyVersion)},{row.TimestampUtc:O}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"policy_compliance_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", fileName);
    }

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

        if (ToUtc.HasValue)
        {
            query = query.Where(x => x.TimestampUtc <= ToUtc.Value);
        }

        return query;
    }

    private static string Escape(string value)
    {
        var safe = value.Replace("\"", "\"\"");
        return $"\"{safe}\"";
    }
}
