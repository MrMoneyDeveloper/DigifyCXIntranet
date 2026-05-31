using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class PoliciesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IZendeskPolicySyncService _syncService;

    public PoliciesModel(ApplicationDbContext db, IZendeskPolicySyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    [TempData]
    public string SyncMessage { get; set; } = string.Empty;

    public List<ZendeskPolicyArticle> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSyncNowAsync()
    {
        await _syncService.SyncAsync();
        SyncMessage = "Zendesk policy sync completed.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await _db.ZendeskPolicyArticles
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync();
    }
}
