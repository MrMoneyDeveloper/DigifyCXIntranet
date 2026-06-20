using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class FaqModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public FaqModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public NewFaqInput NewItem { get; set; } = new();

    public List<FaqItem> Items { get; private set; } = new();

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

        _db.FaqItems.Add(new FaqItem
        {
            Category = NewItem.Category,
            Question = NewItem.Question,
            Answer = NewItem.Answer,
            DisplayOrder = NewItem.DisplayOrder,
            IsActive = true
        });

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var entity = await _db.FaqItems.FirstOrDefaultAsync(x => x.Id == id);
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
        Items = await _db.FaqItems
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.DisplayOrder)
            .ToListAsync();
    }

    public class NewFaqInput
    {
        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = "General";

        [Required]
        [MaxLength(250)]
        public string Question { get; set; } = string.Empty;

        [Required]
        [MaxLength(5000)]
        public string Answer { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }
    }
}
