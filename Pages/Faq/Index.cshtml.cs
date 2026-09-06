using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Faq;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<FaqItem> Items { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 30;
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        PageSize = Math.Clamp(PageSize, 10, 100);
        var query = _db.FaqItems
            .AsNoTracking()
            .Where(x => x.IsActive);
        TotalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, totalPages);
        Items = await query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.DisplayOrder)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
