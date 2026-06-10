using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class JobEditModel : PageModel
{
    private readonly HrDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public JobEditModel(HrDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty]
    public EditJobInput Item { get; set; } = new();

    [BindProperty]
    public IFormFile? AdBackgroundUpload { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        Item = new EditJobInput
        {
            Id = entity.Id,
            Title = entity.Title,
            Department = entity.Department,
            Description = entity.Description,
            ApplicationRoute = entity.ApplicationRoute,
            ClosingDate = entity.ClosingDate,
            IsExternalReferral = entity.IsExternalReferral,
            UseVisualAd = entity.UseVisualAd,
            AdHeadline = entity.AdHeadline,
            AdSubHeadline = entity.AdSubHeadline,
            AdBackgroundImagePath = entity.AdBackgroundImagePath,
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

        var entity = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == Item.Id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        var backgroundPath = Item.AdBackgroundImagePath;
        if (AdBackgroundUpload is { Length: > 0 })
        {
            try
            {
                backgroundPath = await JobAdBackgroundStorage.SaveAsync(AdBackgroundUpload, _environment.WebRootPath);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(AdBackgroundUpload), ex.Message);
                return Page();
            }
        }

        entity.Title = Item.Title;
        entity.Department = Item.Department;
        entity.Description = Item.Description;
        entity.ApplicationRoute = Item.ApplicationRoute;
        entity.ClosingDate = Item.ClosingDate;
        entity.IsExternalReferral = Item.IsExternalReferral;
        entity.UseVisualAd = Item.UseVisualAd;
        entity.AdHeadline = string.IsNullOrWhiteSpace(Item.AdHeadline) ? Item.Title : Item.AdHeadline.Trim();
        entity.AdSubHeadline = string.IsNullOrWhiteSpace(Item.AdSubHeadline) ? Item.Description : Item.AdSubHeadline.Trim();
        entity.AdBackgroundImagePath = string.IsNullOrWhiteSpace(backgroundPath)
            ? "/images/job-ad-a4-template.svg"
            : backgroundPath;
        entity.IsActive = Item.IsActive;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        entity.UpdatedDateUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("/Admin/Jobs");
    }

    public class EditJobInput
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        [MaxLength(8000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ApplicationRoute { get; set; } = string.Empty;

        [Required]
        public DateOnly ClosingDate { get; set; }

        [MaxLength(180)]
        public string AdHeadline { get; set; } = string.Empty;

        [MaxLength(450)]
        public string AdSubHeadline { get; set; } = string.Empty;

        [MaxLength(250)]
        public string AdBackgroundImagePath { get; set; } = "/images/job-ad-a4-template.svg";

        public bool IsExternalReferral { get; set; }
        public bool UseVisualAd { get; set; }
        public bool IsActive { get; set; }
    }
}
