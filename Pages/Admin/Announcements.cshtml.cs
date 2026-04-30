using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class AnnouncementsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public AnnouncementsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public NewAnnouncementInput NewItem { get; set; } = new();

    public List<Announcement> Items { get; private set; } = new();

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

        _db.Announcements.Add(new Announcement
        {
            Title = NewItem.Title,
            Summary = NewItem.Summary,
            Content = NewItem.Content,
            IsPinned = NewItem.IsPinned,
            IsActive = true,
            PublishDateUtc = DateTime.UtcNow,
            LastUpdatedBy = UserNameHelper.GetShortName(User)
        });

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var entity = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = !entity.IsActive;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await _db.Announcements
            .OrderByDescending(x => x.PublishDateUtc)
            .ToListAsync();
    }

    public class NewAnnouncementInput
    {
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Summary { get; set; } = string.Empty;

        [Required]
        [MaxLength(8000)]
        public string Content { get; set; } = string.Empty;

        public bool IsPinned { get; set; }
    }
}
