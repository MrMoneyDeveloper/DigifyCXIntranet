using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class MenuModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public MenuModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public NewMenuItemInput NewItem { get; set; } = new();

    public List<MenuItem> Items { get; private set; } = new();

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

        _db.MenuItems.Add(new MenuItem
        {
            Name = NewItem.Name,
            Price = NewItem.Price,
            Emoji = string.IsNullOrWhiteSpace(NewItem.Emoji) ? "🍽️" : NewItem.Emoji,
            IconClass = NewItem.IconClass,
            MealSlot = NewItem.MealSlot,
            DisplayOrder = NewItem.DisplayOrder,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var entity = await _db.MenuItems.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = !entity.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await _db.MenuItems
            .OrderBy(x => x.MealSlot)
            .ThenBy(x => x.DisplayOrder)
            .ToListAsync();
    }

    public class NewMenuItemInput
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 100000)]
        public decimal Price { get; set; }

        [MaxLength(20)]
        public string Emoji { get; set; } = "🍽️";

        [MaxLength(80)]
        public string IconClass { get; set; } = string.Empty;

        [Required]
        public MealSlot MealSlot { get; set; } = MealSlot.Lunch;

        public int DisplayOrder { get; set; }
    }
}
