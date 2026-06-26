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

    public IndexModel(
        ApplicationDbContext db,
        CanteenDbContext canteenDb,
        HrDbContext hrDb,
        PolicyDbContext policyDb,
        IAdminAccessService adminAccessService,
        IOptions<HomePageOptions> homePageOptions)
    {
        _db = db;
        _canteenDb = canteenDb;
        _hrDb = hrDb;
        _policyDb = policyDb;
        _adminAccessService = adminAccessService;
        _homePageOptions = homePageOptions.Value;
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
        DisplayName = UserNameHelper.GetShortName(User);
        IsAdmin = _adminAccessService.IsAdmin(User);
        RoleName = _adminAccessService.GetPrimaryRole(User);

        Announcements = await _db.Announcements
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && (x.ExpirationDate == null || x.ExpirationDate >= DateTime.UtcNow.Date))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedDateUtc)
            .Take(5)
            .ToListAsync();

        var recentCutoff = DateTime.UtcNow.AddDays(-Math.Clamp(_homePageOptions.RecentJobDays, 1, 365));
        JobPostings = await _hrDb.JobPostings
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && x.CreatedDateUtc >= recentCutoff)
            .OrderByDescending(x => x.CreatedDateUtc)
            .ThenBy(x => x.ClosingDate)
            .Take(5)
            .ToListAsync();

        // Fetch top 5 for the homepage panel display
        Policies = await _policyDb.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(5)
            .ToListAsync();

        // Separate accurate count — not capped by Take(5)
        PolicyCount = await _policyDb.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .CountAsync();

        var username = UserNameHelper.GetShortName(User);
        var today = DateTime.UtcNow;
        CurrentMonthCanteenTotal = await _canteenDb.CanteenOrders
            .AsNoTracking()
            .Where(x => x.EmployeeUsername == username && x.OrderTimeUtc.Year == today.Year && x.OrderTimeUtc.Month == today.Month)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
    }
}
