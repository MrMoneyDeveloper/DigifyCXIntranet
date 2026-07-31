using DigifyCXIntranet.Data;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Profile;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _accountDb;
    private readonly CanteenDbContext _canteenDb;
    private readonly HrDbContext _hrDb;
    private readonly PolicyDbContext _policyDb;
    private readonly IAdminAccessService _adminAccessService;
    private readonly ZendeskSyncOptions _zendeskSyncOptions;

    public IndexModel(
        ApplicationDbContext accountDb,
        CanteenDbContext canteenDb,
        HrDbContext hrDb,
        PolicyDbContext policyDb,
        IAdminAccessService adminAccessService,
        IOptions<ZendeskSyncOptions> zendeskSyncOptions)
    {
        _accountDb = accountDb;
        _canteenDb = canteenDb;
        _hrDb = hrDb;
        _policyDb = policyDb;
        _adminAccessService = adminAccessService;
        _zendeskSyncOptions = zendeskSyncOptions.Value;
    }

    public string DisplayName { get; private set; } = string.Empty;
    public string CompanyLogin { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public bool IsFirstTimeLogin { get; private set; }
    public bool HasPersonalEmail { get; private set; }
    public decimal CurrentMonthCanteenSpend { get; private set; }
    public int PendingPolicyCount { get; private set; }
    public List<CanteenOrderActivity> RecentCanteenOrders { get; private set; } = new();
    public List<JobApplicationActivity> RecentJobApplications { get; private set; } = new();
    public List<PolicyAcknowledgementActivity> RecentPolicyAcknowledgements { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var identityName = User.Identity?.Name ?? string.Empty;
        var username = UserNameHelper.GetShortName(User);
        CompanyLogin = string.IsNullOrWhiteSpace(username) ? "Unknown" : username;
        RoleName = _adminAccessService.GetPrimaryRole(User);

        var normalizedUserName = CompanyLogin.ToUpperInvariant();
        var account = await _accountDb.Users
            .AsNoTracking()
            .Where(x =>
                x.NormalizedUserName == normalizedUserName ||
                x.UserName == CompanyLogin ||
                x.UserName == identityName)
            .Select(x => new
            {
                x.DisplayName,
                x.CustomRole,
                x.IsFirstTimeLogin,
                x.PersonalEmail
            })
            .FirstOrDefaultAsync();

        DisplayName = !string.IsNullOrWhiteSpace(account?.DisplayName)
            ? account.DisplayName
            : CompanyLogin;

        if (!string.IsNullOrWhiteSpace(account?.CustomRole))
        {
            RoleName = account.CustomRole;
        }

        IsFirstTimeLogin = account?.IsFirstTimeLogin ?? false;
        HasPersonalEmail = !string.IsNullOrWhiteSpace(account?.PersonalEmail);

        var now = DateTime.UtcNow;
        CurrentMonthCanteenSpend = await _canteenDb.CanteenOrders
            .AsNoTracking()
            .Where(x =>
                x.EmployeeUsername == CompanyLogin &&
                x.OrderTimeUtc.Year == now.Year &&
                x.OrderTimeUtc.Month == now.Month)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var recentCanteenOrders = await _canteenDb.CanteenOrders
            .AsNoTracking()
            .Where(x => x.EmployeeUsername == CompanyLogin)
            .OrderByDescending(x => x.OrderTimeUtc)
            .Take(5)
            .Select(x => new
            {
                x.ItemSummary,
                x.MealSlot,
                x.TotalAmount,
                x.Status,
                x.OrderTimeUtc
            })
            .ToListAsync();

        RecentCanteenOrders = recentCanteenOrders
            .Select(x => new CanteenOrderActivity(
                x.ItemSummary,
                x.MealSlot.ToString(),
                x.TotalAmount,
                x.Status,
                x.OrderTimeUtc))
            .ToList();

        RecentJobApplications = await _hrDb.InternalJobApplications
            .AsNoTracking()
            .Where(x => x.EmployeeUsername == CompanyLogin)
            .OrderByDescending(x => x.SubmittedUtc)
            .Take(5)
            .Select(x => new JobApplicationActivity(
                x.JobPosting == null ? "Job application" : x.JobPosting.Title,
                x.JobPosting == null ? string.Empty : x.JobPosting.Department,
                x.ZendeskTicketId.HasValue ? "Submitted - ticket created" : "Submitted",
                x.SubmittedUtc))
            .ToListAsync();

        var recentAcknowledgements = await _policyDb.PolicyAcknowledgements
            .AsNoTracking()
            .Where(x => x.EmployeeDomainName == CompanyLogin)
            .OrderByDescending(x => x.TimestampUtc)
            .Take(5)
            .ToListAsync();

        var recentPolicyArticleIds = recentAcknowledgements
            .Select(x => x.PolicyArticleId)
            .Distinct()
            .ToList();

        var recentArticles = await _policyDb.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => recentPolicyArticleIds.Contains(x.ZendeskArticleId))
            .ToDictionaryAsync(x => x.ZendeskArticleId);

        RecentPolicyAcknowledgements = recentAcknowledgements
            .Select(x =>
            {
                recentArticles.TryGetValue(x.PolicyArticleId, out var article);
                return new PolicyAcknowledgementActivity(
                    article?.Title ?? $"Policy article {x.PolicyArticleId}",
                    x.PolicyVersion,
                    x.TimestampUtc);
            })
            .ToList();

        var acknowledgedPolicyArticleIds = await _policyDb.PolicyAcknowledgements
            .AsNoTracking()
            .Where(x => x.EmployeeDomainName == CompanyLogin)
            .Select(x => x.PolicyArticleId)
            .Distinct()
            .ToListAsync();

        PendingPolicyCount = await _policyDb.ZendeskPolicyArticles
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .InAllowedZendeskSections(_zendeskSyncOptions)
            .Where(x => !acknowledgedPolicyArticleIds.Contains(x.ZendeskArticleId))
            .CountAsync();
    }

    public record CanteenOrderActivity(
        string ItemSummary,
        string MealSlot,
        decimal TotalAmount,
        string Status,
        DateTime OrderTimeUtc);

    public record JobApplicationActivity(
        string JobTitle,
        string Department,
        string Status,
        DateTime SubmittedUtc);

    public record PolicyAcknowledgementActivity(
        string PolicyTitle,
        string Version,
        DateTime AcknowledgedUtc);
}
