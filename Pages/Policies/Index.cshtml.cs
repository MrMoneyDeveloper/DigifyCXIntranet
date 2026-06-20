using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Policies;

public class IndexModel : PageModel
{
    private readonly PolicyDbContext _db;

    public IndexModel(PolicyDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<PolicyGroup> Groups { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var query = _db.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
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
