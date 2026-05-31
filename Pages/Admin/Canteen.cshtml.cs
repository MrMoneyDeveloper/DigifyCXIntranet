using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class CanteenModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CanteenModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.Today.Year;

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.Today.Month;

    public List<CanteenOrder> Orders { get; private set; } = new();
    public List<EmployeeTally> Tallies { get; private set; } = new();
    public List<CanteenBatchRun> BatchRuns { get; private set; } = new();
    public List<PayrollRun> PayrollRuns { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var periodStart = new DateTime(Year, Math.Clamp(Month, 1, 12), 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        Orders = await _db.CanteenOrders
            .Where(x => x.OrderTimeUtc >= periodStart && x.OrderTimeUtc < periodEnd)
            .OrderBy(x => x.EmployeeUsername)
            .ThenByDescending(x => x.OrderTimeUtc)
            .ToListAsync();

        Tallies = Orders
            .GroupBy(x => x.EmployeeUsername)
            .Select(x => new EmployeeTally
            {
                EmployeeUsername = x.Key,
                TotalAmount = x.Sum(v => v.TotalAmount),
                Count = x.Count()
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        BatchRuns = await _db.CanteenBatchRuns
            .OrderByDescending(x => x.TriggeredUtc)
            .Take(20)
            .ToListAsync();

        PayrollRuns = await _db.PayrollRuns
            .OrderByDescending(x => x.TriggeredUtc)
            .Take(12)
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetDownloadBatchArtifactAsync(int id)
    {
        var run = await _db.CanteenBatchRuns.FirstOrDefaultAsync(x => x.Id == id);
        if (run is null ||
            string.IsNullOrWhiteSpace(run.ArtifactPath) ||
            !run.ArtifactPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(run.ArtifactPath))
        {
            return NotFound();
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(run.ArtifactPath);
        var fileName = string.IsNullOrWhiteSpace(run.AttachmentFileName)
            ? Path.GetFileName(run.ArtifactPath)
            : run.AttachmentFileName;
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> OnGetDownloadPayrollArtifactAsync(int id)
    {
        var run = await _db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id);
        if (run is null ||
            string.IsNullOrWhiteSpace(run.ArtifactPath) ||
            !run.ArtifactPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(run.ArtifactPath))
        {
            return NotFound();
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(run.ArtifactPath);
        var fileName = string.IsNullOrWhiteSpace(run.AttachmentFileName)
            ? Path.GetFileName(run.ArtifactPath)
            : run.AttachmentFileName;
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public class EmployeeTally
    {
        public string EmployeeUsername { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int Count { get; set; }
    }
}
