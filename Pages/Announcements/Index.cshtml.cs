using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
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

    public async Task OnGetAsync()
    {
        var today = DateTime.UtcNow.Date;
        Items = await _db.Announcements
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && (x.ExpirationDate == null || x.ExpirationDate >= today))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedDateUtc)
            .ToListAsync();
    }
}
