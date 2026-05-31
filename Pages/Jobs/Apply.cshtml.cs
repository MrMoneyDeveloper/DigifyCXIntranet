using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Jobs;

public class ApplyModel : PageModel
{
    private static readonly string[] AllowedResumeExtensions = [".pdf", ".docx"];

    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly RoutingInboxesOptions _inboxes;

    public ApplyModel(
        ApplicationDbContext db,
        IEmailSender emailSender,
        IOptions<RoutingInboxesOptions> inboxOptions)
    {
        _db = db;
        _emailSender = emailSender;
        _inboxes = inboxOptions.Value;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public JobPosting? Job { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Job = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (Job is null)
        {
            return NotFound();
        }

        Input.JobPostingId = id;
        Input.EmployeeName = UserNameHelper.GetShortName(User);
        Input.EmployeeEmail = User.FindFirstValue(ClaimTypes.Email) ?? $"{Input.EmployeeName}@company.local";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Job = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == Input.JobPostingId && x.IsActive);
        if (Job is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (ResumeFile is null || ResumeFile.Length <= 0)
        {
            ModelState.AddModelError(nameof(ResumeFile), "Resume file is required.");
            return Page();
        }

        if (ResumeFile.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(ResumeFile), "Resume file must be 5MB or less.");
            return Page();
        }

        var extension = Path.GetExtension(ResumeFile.FileName).ToLowerInvariant();
        if (!AllowedResumeExtensions.Contains(extension))
        {
            ModelState.AddModelError(nameof(ResumeFile), "Only PDF and DOCX files are allowed.");
            return Page();
        }

        await using var ms = new MemoryStream();
        await ResumeFile.CopyToAsync(ms);
        var bytes = ms.ToArray();

        await _emailSender.SendAsync(new EmailMessage
        {
            To = _inboxes.HrHiringInbox,
            Subject = $"Internal Application: {Job.Title}",
            BodyText = $"Employee: {Input.EmployeeName}\nEmail: {Input.EmployeeEmail}\nJob: {Job.Title}\nNotes: {Input.Notes}",
            Attachments = new List<EmailAttachment>
            {
                new()
                {
                    FileName = ResumeFile.FileName,
                    ContentType = ResumeFile.ContentType,
                    Bytes = bytes
                }
            }
        });

        _db.InternalJobApplications.Add(new InternalJobApplication
        {
            JobPostingId = Job.Id,
            EmployeeUsername = Input.EmployeeName,
            EmployeeEmail = Input.EmployeeEmail,
            Notes = Input.Notes,
            SubmittedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["JobApplyMessage"] = "Application submitted to HR.";
        return RedirectToPage("/Jobs/Index");
    }

    [BindProperty]
    public IFormFile? ResumeFile { get; set; }

    public class InputModel
    {
        public int JobPostingId { get; set; }

        [Required, MaxLength(120)]
        public string EmployeeName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string EmployeeEmail { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }
}
