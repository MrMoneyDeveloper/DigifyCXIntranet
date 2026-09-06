using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Announcements;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Announcement> Items { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateTime.UtcNow.Date;
        PageSize = Math.Clamp(PageSize, 10, 50);
        var query = _db.Announcements
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && (x.ExpirationDate == null || x.ExpirationDate >= today));
        TotalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, totalPages);
        Items = await query
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedDateUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
