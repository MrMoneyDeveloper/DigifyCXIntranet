using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Canteen;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public NewOrderInput NewOrder { get; set; } = new();

    public List<CanteenOrder> MyOrders { get; private set; } = new();
    public List<MonthlyTallyItem> MonthlyTallies { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSubmitAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var username = UserNameHelper.GetShortName(User);
        _db.CanteenOrders.Add(new CanteenOrder
        {
            EmployeeUsername = username,
            ItemSummary = NewOrder.ItemSummary,
            OrderDate = NewOrder.OrderDate,
            TotalAmount = NewOrder.TotalAmount,
            EmploymentMonthsAtOrder = NewOrder.EmploymentMonthsAtOrder,
            Status = "Submitted",
            IncludedInPayrollReconciliation = true
        });

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var username = UserNameHelper.GetShortName(User);
        MyOrders = await _db.CanteenOrders
            .Where(x => x.EmployeeUsername == username)
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.Id)
            .Take(50)
            .ToListAsync();

        MonthlyTallies = await _db.CanteenOrders
            .Where(x => x.EmployeeUsername == username)
            .GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
            .Select(x => new MonthlyTallyItem
            {
                Year = x.Key.Year,
                Month = x.Key.Month,
                TotalAmount = x.Sum(v => v.TotalAmount)
            })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Take(12)
            .ToListAsync();

        if (NewOrder.OrderDate == default)
        {
            NewOrder.OrderDate = DateOnly.FromDateTime(DateTime.Today);
        }
    }

    public class NewOrderInput
    {
        [Required]
        [MaxLength(300)]
        public string ItemSummary { get; set; } = string.Empty;

        [Required]
        public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Range(0.01, 100000)]
        public decimal TotalAmount { get; set; }

        [Range(0, 120)]
        public int EmploymentMonthsAtOrder { get; set; }
    }

    public class MonthlyTallyItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
