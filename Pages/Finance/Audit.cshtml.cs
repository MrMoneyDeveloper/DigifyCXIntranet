using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
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

    public async Task OnGetAsync()
    {
        Items = await _db.FinanceAuditLogs
            .OrderByDescending(x => x.TimestampUtc)
            .Take(500)
            .ToListAsync();
    }
}
