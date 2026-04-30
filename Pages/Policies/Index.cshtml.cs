using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Policies;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<PolicyDocument> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var query = _db.PolicyDocuments
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveDate)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(x => x.Title.Contains(Search) || x.Content.Contains(Search));
        }

        Items = await query.ToListAsync();
    }
}
