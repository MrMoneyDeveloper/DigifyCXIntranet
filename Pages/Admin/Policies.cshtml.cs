using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class PoliciesModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public PoliciesModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public NewPolicyInput NewPolicy { get; set; } = new();

    public List<PolicyDocument> Items { get; private set; } = new();

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

        _db.PolicyDocuments.Add(new PolicyDocument
        {
            Title = NewPolicy.Title,
            ContentType = NewPolicy.ContentType,
            VersionLabel = NewPolicy.VersionLabel,
            EffectiveDate = NewPolicy.EffectiveDate,
            Content = NewPolicy.Content,
            IsActive = true,
            LastUpdatedBy = UserNameHelper.GetShortName(User),
            LastUpdatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var item = await _db.PolicyDocuments.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        item.IsActive = !item.IsActive;
        item.LastUpdatedBy = UserNameHelper.GetShortName(User);
        item.LastUpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await _db.PolicyDocuments
            .OrderByDescending(x => x.LastUpdatedUtc)
            .ToListAsync();
    }

    public class NewPolicyInput
    {
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public PolicyContentType ContentType { get; set; } = PolicyContentType.Policy;

        [Required]
        [MaxLength(30)]
        public string VersionLabel { get; set; } = "v1.0";

        [Required]
        public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [MaxLength(8000)]
        public string Content { get; set; } = string.Empty;
    }
}
