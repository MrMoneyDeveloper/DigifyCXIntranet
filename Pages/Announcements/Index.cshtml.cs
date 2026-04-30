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
        Items = await _db.Announcements
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.PublishDateUtc)
            .ToListAsync();
    }
}
