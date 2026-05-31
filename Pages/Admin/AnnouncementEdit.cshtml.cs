using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class AnnouncementEditModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public AnnouncementEditModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public EditAnnouncementInput Item { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        Item = new EditAnnouncementInput
        {
            Id = entity.Id,
            Title = entity.Title,
            Summary = entity.Summary,
            Content = entity.Content,
            IsPinned = entity.IsPinned,
            ExpirationDate = entity.ExpirationDate,
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

        var entity = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == Item.Id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Title = Item.Title;
        entity.Summary = Item.Summary;
        entity.Content = Item.Content;
        entity.IsPinned = Item.IsPinned;
        entity.ExpirationDate = Item.ExpirationDate;
        entity.IsActive = Item.IsActive;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Admin/Announcements");
    }

    public class EditAnnouncementInput
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Summary { get; set; } = string.Empty;

        [Required]
        [MaxLength(8000)]
        public string Content { get; set; } = string.Empty;

        public bool IsPinned { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool IsActive { get; set; }
    }
}
