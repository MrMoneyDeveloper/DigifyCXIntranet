using DigifyCXIntranet.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public int PolicyCount { get; private set; }
    public int AnnouncementCount { get; private set; }
    public int JobCount { get; private set; }
    public int FaqCount { get; private set; }
    public int CanteenOrderCount { get; private set; }

    public async Task OnGetAsync()
    {
        PolicyCount = await _db.PolicyDocuments.CountAsync();
        AnnouncementCount = await _db.Announcements.CountAsync();
        JobCount = await _db.JobPostings.CountAsync();
        FaqCount = await _db.FaqItems.CountAsync();
        CanteenOrderCount = await _db.CanteenOrders.CountAsync();
    }
}
