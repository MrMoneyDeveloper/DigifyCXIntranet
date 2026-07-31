using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class MenuModel : PageModel
{
    private readonly CanteenDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IFinanceAuditService _auditService;

    public MenuModel(CanteenDbContext db, IWebHostEnvironment environment, IFinanceAuditService auditService)
    {
        _db = db;
        _environment = environment;
        _auditService = auditService;
    }

    [BindProperty]
    public NewMenuItemInput NewItem { get; set; } = new();

    [BindProperty]
    public IFormFile? FoodImageUpload { get; set; }

    public List<MenuItem> Items { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 100;

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        if (FoodImageUpload is null || FoodImageUpload.Length <= 0)
        {
            ModelState.AddModelError(nameof(FoodImageUpload), "A food image is required for new menu items.");
            await LoadAsync();
            return Page();
        }

        string imagePath;
        try
        {
            imagePath = await MenuItemImageStorage.SaveAsync(FoodImageUpload, _environment.WebRootPath);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(FoodImageUpload), ex.Message);
            await LoadAsync();
            return Page();
        }

        var item = new MenuItem
        {
            Name = NewItem.Name.Trim(),
            Price = NewItem.Price,
            Emoji = string.IsNullOrWhiteSpace(NewItem.Emoji) ? "\U0001F37D" : NewItem.Emoji.Trim(),
            IconClass = NewItem.IconClass.Trim(),
            ImagePath = imagePath,
            MealSlot = NewItem.MealSlot,
            DisplayOrder = NewItem.DisplayOrder,
            IsActive = true
        };

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Create", "MenuItem", $"id={item.Id};name={item.Name};price={item.Price};meal={item.MealSlot}");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var entity = await _db.MenuItems.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = !entity.IsActive;
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), entity.IsActive ? "Activate" : "Deactivate", "MenuItem", $"id={entity.Id};name={entity.Name}");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var entity = await _db.MenuItems.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        if (entity.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Deactivate the menu item before deleting it.");
            await LoadAsync();
            return Page();
        }

        entity.IsDeleted = true;
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Delete", "MenuItem", $"id={entity.Id};name={entity.Name};softDelete=true");
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 25, 200);
        Items = await _db.MenuItems
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.MealSlot)
            .ThenBy(x => x.DisplayOrder)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public class NewMenuItemInput
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 100000)]
        public decimal Price { get; set; }

        [MaxLength(20)]
        public string Emoji { get; set; } = "\U0001F37D";

        [MaxLength(80)]
        public string IconClass { get; set; } = string.Empty;

        [Required]
        public MealSlot MealSlot { get; set; } = MealSlot.Lunch;

        public int DisplayOrder { get; set; }
    }
}
