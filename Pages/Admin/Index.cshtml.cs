using DigifyCXIntranet.Data;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly CanteenDbContext _canteenDb;
    private readonly HrDbContext _hrDb;
    private readonly PolicyDbContext _policyDb;
    private readonly IAuthorizationService _authorizationService;
    private readonly ZendeskSyncOptions _zendeskSyncOptions;

    public IndexModel(
        ApplicationDbContext db,
        CanteenDbContext canteenDb,
        HrDbContext hrDb,
        PolicyDbContext policyDb,
        IAuthorizationService authorizationService,
        IOptions<ZendeskSyncOptions> zendeskSyncOptions)
    {
        _db = db;
        _canteenDb = canteenDb;
        _hrDb = hrDb;
        _policyDb = policyDb;
        _authorizationService = authorizationService;
        _zendeskSyncOptions = zendeskSyncOptions.Value;
    }

    public int PolicyCount { get; private set; }
    public int AnnouncementCount { get; private set; }
    public int JobCount { get; private set; }
    public int FaqCount { get; private set; }
    public int CanteenOrderCount { get; private set; }
    public int MenuItemCount { get; private set; }
    public int BatchRunCount { get; private set; }
    public bool CanManageCanteen { get; private set; }
    public bool CanManageHr { get; private set; }
    public bool CanManageSystem { get; private set; }
    public bool CanManageAnnouncements { get; private set; }
    public bool CanManageUsers { get; private set; }
    public bool CanViewFinance { get; private set; }

    public async Task OnGetAsync()
    {
        CanManageCanteen = (await _authorizationService.AuthorizeAsync(User, AppPolicies.CanteenOperations)).Succeeded;
        CanManageHr = (await _authorizationService.AuthorizeAsync(User, AppPolicies.HrOperations)).Succeeded;
        CanManageSystem = (await _authorizationService.AuthorizeAsync(User, AppPolicies.SystemOperations)).Succeeded;
        CanManageAnnouncements = (await _authorizationService.AuthorizeAsync(User, AppPolicies.AnnouncementManagement)).Succeeded;
        CanManageUsers = (await _authorizationService.AuthorizeAsync(User, AppPolicies.UserManagement)).Succeeded;
        CanViewFinance = (await _authorizationService.AuthorizeAsync(User, AppPolicies.FinanceLedger)).Succeeded;

        PolicyCount = await _policyDb.ZendeskPolicyArticles
            .InAllowedZendeskSections(_zendeskSyncOptions)
            .CountAsync();
        AnnouncementCount = await _db.Announcements.CountAsync(x => !x.IsDeleted);
        JobCount = await _hrDb.JobPostings.CountAsync(x => !x.IsDeleted);
        FaqCount = await _db.FaqItems.CountAsync();
        CanteenOrderCount = await _canteenDb.CanteenOrders.CountAsync();
        MenuItemCount = await _canteenDb.MenuItems.CountAsync(x => !x.IsDeleted);
        BatchRunCount = await _canteenDb.CanteenBatchRuns.CountAsync();
    }
}
