using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class MenuEditModel : PageModel
{
    private readonly CanteenDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IFinanceAuditService _auditService;

    public MenuEditModel(CanteenDbContext db, IWebHostEnvironment environment, IFinanceAuditService auditService)
    {
        _db = db;
        _environment = environment;
        _auditService = auditService;
    }

    [BindProperty]
    public EditMenuItemInput Item { get; set; } = new();

    [BindProperty]
    public IFormFile? FoodImageUpload { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.MenuItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        Item = new EditMenuItemInput
        {
            Id = entity.Id,
            Name = entity.Name,
            Price = entity.Price,
            Emoji = entity.Emoji,
            IconClass = entity.IconClass,
            ImagePath = entity.ImagePath,
            MealSlot = entity.MealSlot,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var entity = await _db.MenuItems.FirstOrDefaultAsync(x => x.Id == Item.Id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        string? replacementImagePath = null;
        if (FoodImageUpload is { Length: > 0 })
        {
            try
            {
                replacementImagePath = await MenuItemImageStorage.SaveAsync(FoodImageUpload, _environment.WebRootPath);
            }
            catch (InvalidOperationException ex)
            {
                Item.ImagePath = entity.ImagePath;
                ModelState.AddModelError(nameof(FoodImageUpload), ex.Message);
                return Page();
            }
        }

        entity.Name = Item.Name.Trim();
        entity.Price = Item.Price;
        entity.Emoji = string.IsNullOrWhiteSpace(Item.Emoji) ? "\U0001F37D" : Item.Emoji.Trim();
        entity.IconClass = Item.IconClass?.Trim() ?? string.Empty;
        entity.ImagePath = ResolveImagePath(entity.ImagePath, replacementImagePath);
        entity.MealSlot = Item.MealSlot;
        entity.IsActive = Item.IsActive;
        entity.DisplayOrder = Item.DisplayOrder;

        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Edit", "MenuItem", $"id={entity.Id};name={entity.Name};price={entity.Price};active={entity.IsActive}");
        return RedirectToPage("/Admin/Menu");
    }

    public static string ResolveImagePath(string? existingImagePath, string? replacementImagePath)
    {
        return string.IsNullOrWhiteSpace(replacementImagePath)
            ? existingImagePath?.Trim() ?? string.Empty
            : replacementImagePath.Trim();
    }

    public class EditMenuItemInput
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 100000)]
        public decimal Price { get; set; }

        [MaxLength(20)]
        public string Emoji { get; set; } = "\U0001F37D";

        [MaxLength(80)]
        public string IconClass { get; set; } = string.Empty;

        [MaxLength(250)]
        public string ImagePath { get; set; } = string.Empty;

        [Required]
        public MealSlot MealSlot { get; set; } = MealSlot.Lunch;

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
    }
}
