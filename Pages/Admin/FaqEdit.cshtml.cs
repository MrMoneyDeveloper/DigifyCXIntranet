using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class FaqEditModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public FaqEditModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public EditFaqInput Item { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.FaqItems.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        Item = new EditFaqInput
        {
            Id = entity.Id,
            Category = entity.Category,
            Question = entity.Question,
            Answer = entity.Answer,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var entity = await _db.FaqItems.FirstOrDefaultAsync(x => x.Id == Item.Id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Category = Item.Category;
        entity.Question = Item.Question;
        entity.Answer = Item.Answer;
        entity.DisplayOrder = Item.DisplayOrder;
        entity.IsActive = Item.IsActive;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Admin/Faq");
    }

    public class EditFaqInput
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(80)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Question { get; set; } = string.Empty;

        [Required]
        [MaxLength(5000)]
        public string Answer { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
