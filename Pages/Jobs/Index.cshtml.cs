using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Jobs;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<JobPosting> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Items = await _db.JobPostings
            .Where(x => x.IsActive)
            .OrderBy(x => x.ClosingDate)
            .ToListAsync();
    }
}
