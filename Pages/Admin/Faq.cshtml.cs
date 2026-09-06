using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class FaqModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public FaqModel(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [BindProperty]
    public NewFaqInput NewItem { get; set; } = new();

    public List<FaqItem> Items { get; private set; } = new();
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

        var item = new FaqItem
        {
            Category = NewItem.Category,
            Question = NewItem.Question,
            Answer = NewItem.Answer,
            DisplayOrder = NewItem.DisplayOrder,
            IsActive = true
        };
        _db.FaqItems.Add(item);

        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(
            UserNameHelper.GetShortName(User),
            "Create",
            "FaqItem",
            $"category={item.Category}",
            entityId: item.Id.ToString(),
            httpContext: HttpContext);
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
        await _auditService.WriteAsync(
            UserNameHelper.GetShortName(User),
            entity.IsActive ? "Activate" : "Deactivate",
            "FaqItem",
            $"category={entity.Category}",
            entityId: entity.Id.ToString(),
            httpContext: HttpContext);
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        PageSize = Math.Clamp(PageSize, 10, 100);
        TotalCount = await _db.FaqItems.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, totalPages);
        Items = await _db.FaqItems
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.DisplayOrder)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
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
