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

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
    {
        var entity = await _db.CanteenOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            entity.Status = status;
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { Year, Month });
    }

    private async Task LoadAsync()
    {
        Orders = await _db.CanteenOrders
            .Where(x => x.OrderDate.Year == Year && x.OrderDate.Month == Month)
            .OrderBy(x => x.EmployeeUsername)
            .ThenByDescending(x => x.OrderDate)
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
    }

    public class EmployeeTally
    {
        public string EmployeeUsername { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int Count { get; set; }
    }
}
