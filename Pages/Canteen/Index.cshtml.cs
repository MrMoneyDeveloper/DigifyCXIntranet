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
    public int SelectedMenuItemId { get; set; }

    public List<MenuItem> MenuItems { get; private set; } = new();
    public List<CanteenOrder> MyOrders { get; private set; } = new();
    public List<MonthlyTallyItem> MonthlyTallies { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSubmitAsync()
    {
        var menuItem = await _db.MenuItems
            .Where(x => x.IsActive && x.Id == SelectedMenuItemId)
            .FirstOrDefaultAsync();

        if (menuItem is null)
        {
            ModelState.AddModelError(string.Empty, "Please select a valid menu item.");
            await LoadAsync();
            return Page();
        }

        var username = UserNameHelper.GetShortName(User);
        _db.CanteenOrders.Add(new CanteenOrder
        {
            EmployeeUsername = username,
            MenuItemId = menuItem.Id,
            ItemSummary = menuItem.Name,
            MealSlot = menuItem.MealSlot,
            TotalAmount = menuItem.Price,
            OrderTimeUtc = DateTime.UtcNow,
            Status = "Submitted"
        });
        await _db.SaveChangesAsync();

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var username = UserNameHelper.GetShortName(User);

        MenuItems = await _db.MenuItems
            .Where(x => x.IsActive)
            .OrderBy(x => x.MealSlot)
            .ThenBy(x => x.DisplayOrder)
            .ToListAsync();

        MyOrders = await _db.CanteenOrders
            .Where(x => x.EmployeeUsername == username)
            .OrderByDescending(x => x.OrderTimeUtc)
            .Take(50)
            .ToListAsync();

        MonthlyTallies = await _db.CanteenOrders
            .Where(x => x.EmployeeUsername == username)
            .GroupBy(x => new { x.OrderTimeUtc.Year, x.OrderTimeUtc.Month })
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
    }

    public class MonthlyTallyItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
