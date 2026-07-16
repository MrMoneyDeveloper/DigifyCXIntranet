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

        var items = await query
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.SectionName)
            .ThenBy(x => x.Title)
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

    public record PolicyGroup(string Category, string Section, List<ZendeskPolicyArticle> Items);
}
