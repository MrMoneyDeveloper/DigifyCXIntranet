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
    private readonly IAuditService _auditService;

    public AnnouncementsModel(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [BindProperty]
    public NewAnnouncementInput NewItem { get; set; } = new();

    public List<AnnouncementRow> Items { get; private set; } = new();
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; private set; }

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

        var announcement = new Announcement
        {
            Title = NewItem.Title,
            Summary = NewItem.Summary,
            Content = NewItem.Content,
            IsPinned = NewItem.IsPinned,
            IsActive = true,
            CreatedDateUtc = DateTime.UtcNow,
            PublishDateUtc = DateTime.UtcNow,
            ExpirationDate = NewItem.ExpirationDate,
            LastUpdatedBy = UserNameHelper.GetShortName(User)
        };
        _db.Announcements.Add(announcement);

        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Create", "Announcement", $"title={announcement.Title}", true, announcement.Id.ToString(), httpContext: HttpContext);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var entity = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = !entity.IsActive;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), entity.IsActive ? "Activate" : "Deactivate", "Announcement", $"title={entity.Title}", true, entity.Id.ToString(), httpContext: HttpContext);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var entity = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        entity.IsDeleted = true;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "Delete", "Announcement", $"title={entity.Title};softDelete=true", true, entity.Id.ToString(), httpContext: HttpContext);
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 10, 100);
        var query = _db.Announcements
            .AsNoTracking()
            .Where(x => !x.IsDeleted);
        TotalCount = await query.CountAsync();
        PageNumber = Math.Min(PageNumber, Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize)));
        Items = await query
            .OrderByDescending(x => x.PublishDateUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new AnnouncementRow(
                x.Id,
                x.Title,
                x.PublishDateUtc,
                x.ExpirationDate,
                x.IsActive))
            .ToListAsync();
    }

    public sealed record AnnouncementRow(
        int Id,
        string Title,
        DateTime PublishDateUtc,
        DateTime? ExpirationDate,
        bool IsActive);

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
        public DateTime? ExpirationDate { get; set; }
    }
}
