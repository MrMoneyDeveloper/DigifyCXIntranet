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
    private readonly IZendeskHtmlSanitizer _htmlSanitizer;
    private readonly ZendeskSyncOptions _options;
    private readonly IAuditService _auditService;

    public PoliciesModel(
        PolicyDbContext db,
        IZendeskPolicySyncService syncService,
        IZendeskHtmlSanitizer htmlSanitizer,
        IOptions<ZendeskSyncOptions> options,
        IAuditService auditService)
    {
        _db = db;
        _syncService = syncService;
        _htmlSanitizer = htmlSanitizer;
        _options = options.Value;
        _auditService = auditService;
    }

    [TempData]
    public string SyncMessage { get; set; } = string.Empty;

    public List<PolicyAdminRow> Items { get; private set; } = new();
    public List<ZendeskSyncLog> RecentSyncLogs { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; private set; }
    public string ConfiguredSubdomain => string.IsNullOrWhiteSpace(_options.Subdomain) ? _options.BaseUrl : _options.Subdomain;
    public long TicketFormId => _options.InternalSupportTicketFormId;
    public bool TokenConfigured => !string.IsNullOrWhiteSpace(_options.ApiToken);

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSyncNowAsync()
    {
        try
        {
            await _syncService.SyncAsync(HttpContext.RequestAborted);
            await _auditService.WriteAsync(
                UserNameHelper.GetShortName(User),
                "ManualSync",
                "ZendeskPolicies",
                "completed",
                httpContext: HttpContext);
            SyncMessage = "Zendesk policy sync completed.";
        }
        catch (Exception)
        {
            await _auditService.WriteAsync(
                UserNameHelper.GetShortName(User),
                "ManualSync",
                "ZendeskPolicies",
                "failed",
                succeeded: false,
                errorCode: "SyncFailed",
                httpContext: HttpContext,
                cancellationToken: CancellationToken.None);
            SyncMessage = "Zendesk policy sync failed. Review the application logs and try again.";
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        PageSize = Math.Clamp(PageSize, 10, 100);
        var query = _db.ZendeskPolicyArticles
            .AsNoTracking()
            .InAllowedZendeskSections(_options);
        TotalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, totalPages);
        var rows = await query
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.SectionName)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new PolicyAdminRow(
                x.Title,
                x.SectionName,
                x.HtmlUrl,
                x.IsPublished,
                x.UpdatedAtUtc))
            .ToListAsync();
        Items = rows
            .Select(item => item with { HtmlUrl = _htmlSanitizer.SanitizeHttpsUrl(item.HtmlUrl) ?? "#" })
            .ToList();

        RecentSyncLogs = await _db.ZendeskSyncLogs
            .AsNoTracking()
            .OrderByDescending(x => x.StartedUtc)
            .Take(5)
            .ToListAsync();
    }

    public sealed record PolicyAdminRow(
        string Title,
        string SectionName,
        string HtmlUrl,
        bool IsPublished,
        DateTime UpdatedAtUtc);
}
