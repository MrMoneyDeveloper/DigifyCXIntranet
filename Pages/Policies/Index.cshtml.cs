using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Policies;

public class IndexModel : PageModel
{
    private readonly PolicyDbContext _db;
    private readonly ZendeskSyncOptions _zendeskSyncOptions;

    public IndexModel(PolicyDbContext db, IOptions<ZendeskSyncOptions> zendeskSyncOptions)
    {
        _db = db;
        _zendeskSyncOptions = zendeskSyncOptions.Value;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; private set; }

    public List<PolicyGroup> Groups { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var query = _db.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .InAllowedZendeskSections(_zendeskSyncOptions)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x => x.Title.Contains(Search) || x.Body.Contains(Search));
        }

        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 10, 100);
        TotalCount = await query.CountAsync();
        PageNumber = Math.Min(PageNumber, Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize)));
        var items = await query
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.SectionName)
            .ThenBy(x => x.Title)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new PolicyListItem(
                x.Id,
                x.Title,
                x.CategoryName,
                x.SectionName,
                x.UpdatedAtUtc))
            .ToListAsync();

        Groups = items
            .GroupBy(x => new
            {
                Category = string.IsNullOrWhiteSpace(x.CategoryName) ? "Uncategorised" : x.CategoryName,
                Section = string.IsNullOrWhiteSpace(x.SectionName) ? "General" : x.SectionName
            })
            .Select(x => new PolicyGroup(x.Key.Category, x.Key.Section, x.ToList()))
            .ToList();
    }

    public sealed record PolicyListItem(
        int Id,
        string Title,
        string CategoryName,
        string SectionName,
        DateTime UpdatedAtUtc);

    public record PolicyGroup(string Category, string Section, List<PolicyListItem> Items);
}
