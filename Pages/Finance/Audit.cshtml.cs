using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Finance;

public class AuditModel : PageModel
{
    private readonly CanteenDbContext _db;

    public AuditModel(CanteenDbContext db)
    {
        _db = db;
    }

    public List<FinanceAuditLog> Items { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 100;
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 25, 200);
        var query = _db.FinanceAuditLogs.AsNoTracking();
        TotalCount = await query.CountAsync();
        PageNumber = Math.Min(PageNumber, Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize)));
        Items = await query
            .OrderByDescending(x => x.TimestampUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
