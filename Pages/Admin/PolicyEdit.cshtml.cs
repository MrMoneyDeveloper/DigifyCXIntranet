using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class PolicyEditModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public PolicyEditModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public EditPolicyInput Item { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.PolicyDocuments.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        Item = new EditPolicyInput
        {
            Id = entity.Id,
            Title = entity.Title,
            ContentType = entity.ContentType,
            VersionLabel = entity.VersionLabel,
            EffectiveDate = entity.EffectiveDate,
            Content = entity.Content,
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

        var entity = await _db.PolicyDocuments.FirstOrDefaultAsync(x => x.Id == Item.Id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Title = Item.Title;
        entity.ContentType = Item.ContentType;
        entity.VersionLabel = Item.VersionLabel;
        entity.EffectiveDate = Item.EffectiveDate;
        entity.Content = Item.Content;
        entity.IsActive = Item.IsActive;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        entity.LastUpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Admin/Policies");
    }

    public class EditPolicyInput
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public PolicyContentType ContentType { get; set; }

        [Required]
        [MaxLength(30)]
        public string VersionLabel { get; set; } = string.Empty;

        [Required]
        public DateOnly EffectiveDate { get; set; }

        [Required]
        [MaxLength(8000)]
        public string Content { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
