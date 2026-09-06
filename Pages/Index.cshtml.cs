using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly CanteenDbContext _canteenDb;
    private readonly HrDbContext _hrDb;
    private readonly PolicyDbContext _policyDb;
    private readonly IAdminAccessService _adminAccessService;
    private readonly HomePageOptions _homePageOptions;
    private readonly ZendeskSyncOptions _zendeskSyncOptions;

    public IndexModel(
        ApplicationDbContext db,
        CanteenDbContext canteenDb,
        HrDbContext hrDb,
        PolicyDbContext policyDb,
        IAdminAccessService adminAccessService,
        IOptions<HomePageOptions> homePageOptions,
        IOptions<ZendeskSyncOptions> zendeskSyncOptions)
    {
        _db = db;
        _canteenDb = canteenDb;
        _hrDb = hrDb;
        _policyDb = policyDb;
        _adminAccessService = adminAccessService;
        _homePageOptions = homePageOptions.Value;
        _zendeskSyncOptions = zendeskSyncOptions.Value;
    }

    public bool IsAdmin { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public decimal CurrentMonthCanteenTotal { get; private set; }
    public List<Announcement> Announcements { get; private set; } = new();
    public List<JobPosting> JobPostings { get; private set; } = new();
    public List<ZendeskPolicyArticle> Policies { get; private set; } = new();
    public int PolicyCount { get; private set; }

    public async Task OnGetAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;
        var nowUtc = DateTime.UtcNow;
        DisplayName = UserNameHelper.GetShortName(User);
        IsAdmin = _adminAccessService.IsAdmin(User);
        RoleName = _adminAccessService.GetPrimaryRole(User);

        Announcements = await _db.Announcements
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && (x.ExpirationDate == null || x.ExpirationDate >= nowUtc.Date))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedDateUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentCutoff = nowUtc.AddDays(-Math.Clamp(_homePageOptions.RecentJobDays, 1, 365));
        JobPostings = await _hrDb.JobPostings
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && x.CreatedDateUtc >= recentCutoff)
            .OrderByDescending(x => x.CreatedDateUtc)
            .ThenBy(x => x.ClosingDate)
            .Take(5)
            .ToListAsync(cancellationToken);

        // Fetch top 5 for the homepage panel display
        Policies = await _policyDb.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .InAllowedZendeskSections(_zendeskSyncOptions)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        // Separate accurate count — not capped by Take(5)
        PolicyCount = await _policyDb.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .InAllowedZendeskSections(_zendeskSyncOptions)
            .CountAsync(cancellationToken);

        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);
        CurrentMonthCanteenTotal = await _canteenDb.CanteenOrders
            .AsNoTracking()
            .Where(x => x.EmployeeUsername == DisplayName && x.OrderTimeUtc >= monthStart && x.OrderTimeUtc < nextMonthStart)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
    }
}
