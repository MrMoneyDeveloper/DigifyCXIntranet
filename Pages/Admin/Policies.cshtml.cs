using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Admin;

public class PoliciesModel : PageModel
{
    private readonly PolicyDbContext _db;
    private readonly IZendeskPolicySyncService _syncService;
    private readonly ZendeskSyncOptions _options;

    public PoliciesModel(PolicyDbContext db, IZendeskPolicySyncService syncService, IOptions<ZendeskSyncOptions> options)
    {
        _db = db;
        _syncService = syncService;
        _options = options.Value;
    }

    [TempData]
    public string SyncMessage { get; set; } = string.Empty;

    public List<ZendeskPolicyArticle> Items { get; private set; } = new();
    public List<ZendeskSyncLog> RecentSyncLogs { get; private set; } = new();
    public string ConfiguredSubdomain => string.IsNullOrWhiteSpace(_options.Subdomain) ? _options.BaseUrl : _options.Subdomain;
    public long TicketFormId => _options.InternalSupportTicketFormId;
    public bool TokenConfigured => !string.IsNullOrWhiteSpace(_options.ApiToken);

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
            .AsNoTracking()
            .InAllowedZendeskSections(_options)
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.SectionName)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToListAsync();

        RecentSyncLogs = await _db.ZendeskSyncLogs
            .AsNoTracking()
            .OrderByDescending(x => x.StartedUtc)
            .Take(5)
            .ToListAsync();
    }
}
