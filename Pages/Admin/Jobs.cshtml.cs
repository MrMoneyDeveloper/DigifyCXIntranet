using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Admin;

public class JobsModel : PageModel
{
    private readonly HrDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public JobsModel(HrDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty]
    public NewJobInput NewItem { get; set; } = new();

    [BindProperty]
    public IFormFile? AdBackgroundUpload { get; set; }

    public List<JobPosting> Items { get; private set; } = new();

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

        var backgroundPath = "/images/job-ad-a4-template.svg";
        if (AdBackgroundUpload is { Length: > 0 })
        {
            try
            {
                backgroundPath = await JobAdBackgroundStorage.SaveAsync(AdBackgroundUpload, _environment.WebRootPath);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(AdBackgroundUpload), ex.Message);
                await LoadAsync();
                return Page();
            }
        }

        _db.JobPostings.Add(new JobPosting
        {
            Title = NewItem.Title,
            Department = NewItem.Department,
            Description = NewItem.Description,
            ApplicationRoute = NewItem.ApplicationRoute,
            ClosingDate = NewItem.ClosingDate,
            IsExternalReferral = NewItem.IsExternalReferral,
            UseVisualAd = NewItem.UseVisualAd,
            AdHeadline = string.IsNullOrWhiteSpace(NewItem.AdHeadline) ? NewItem.Title : NewItem.AdHeadline.Trim(),
            AdSubHeadline = string.IsNullOrWhiteSpace(NewItem.AdSubHeadline) ? NewItem.Description : NewItem.AdSubHeadline.Trim(),
            AdBackgroundImagePath = backgroundPath,
            IsActive = true,
            LastUpdatedBy = UserNameHelper.GetShortName(User),
            CreatedDateUtc = DateTime.UtcNow,
            UpdatedDateUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var entity = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = !entity.IsActive;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        entity.UpdatedDateUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var entity = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (entity is null)
        {
            return NotFound();
        }

        if (entity.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Deactivate the job posting before deleting it.");
            await LoadAsync();
            return Page();
        }

        entity.IsDeleted = true;
        entity.LastUpdatedBy = UserNameHelper.GetShortName(User);
        entity.UpdatedDateUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await _db.JobPostings
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.ClosingDate)
            .ToListAsync();
    }

    public class NewJobInput
    {
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
        public string ApplicationRoute { get; set; } = "hr@digifycx.example";

        [Required]
        public DateOnly ClosingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(14));

        [MaxLength(180)]
        public string AdHeadline { get; set; } = string.Empty;

        [MaxLength(450)]
        public string AdSubHeadline { get; set; } = string.Empty;

        public bool UseVisualAd { get; set; } = true;
        public bool IsExternalReferral { get; set; }
    }
}
