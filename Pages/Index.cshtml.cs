using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminAccessService _adminAccessService;

    public IndexModel(ApplicationDbContext db, IAdminAccessService adminAccessService)
    {
        _db = db;
        _adminAccessService = adminAccessService;
    }

    public bool IsAdmin { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public decimal CurrentMonthCanteenTotal { get; private set; }
    public List<Announcement> Announcements { get; private set; } = new();
    public List<JobPosting> JobPostings { get; private set; } = new();
    public List<PolicyDocument> Policies { get; private set; } = new();

    public async Task OnGetAsync()
    {
        DisplayName = UserNameHelper.GetShortName(User);
        IsAdmin = _adminAccessService.IsAdmin(User);

        Announcements = await _db.Announcements
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.PublishDateUtc)
            .Take(5)
            .ToListAsync();

        JobPostings = await _db.JobPostings
            .Where(x => x.IsActive)
            .OrderBy(x => x.ClosingDate)
            .Take(5)
            .ToListAsync();

        Policies = await _db.PolicyDocuments
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveDate)
            .Take(5)
            .ToListAsync();

        var username = UserNameHelper.GetShortName(User);
        var today = DateOnly.FromDateTime(DateTime.Today);
        CurrentMonthCanteenTotal = await _db.CanteenOrders
            .Where(x => x.EmployeeUsername == username && x.OrderDate.Year == today.Year && x.OrderDate.Month == today.Month)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
    }
}
