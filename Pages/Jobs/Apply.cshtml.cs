using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Jobs;

public class ApplyModel : PageModel
{
    private static readonly string[] AllowedResumeExtensions = [".pdf", ".docx"];

    private readonly HrDbContext _db;
    private readonly IZendeskTicketService _zendeskTicketService;
    private readonly IFinanceAuditService _auditService;

    public ApplyModel(
        HrDbContext db,
        IZendeskTicketService zendeskTicketService,
        IFinanceAuditService auditService)
    {
        _db = db;
        _zendeskTicketService = zendeskTicketService;
        _auditService = auditService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public JobPosting? Job { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Job = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted);
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
        Job = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == Input.JobPostingId && x.IsActive && !x.IsDeleted);
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

        var result = await _zendeskTicketService.CreateInternalApplicationTicketAsync(
            Job,
            Input.EmployeeName,
            Input.EmployeeEmail,
            Input.EmployeeId,
            Input.ManagerEmail,
            Input.Notes,
            ResumeFile);

        if (!result.Succeeded)
        {
            await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "ZendeskTicketFailed", "InternalJobApplication", $"job={Job.Id};employee={Input.EmployeeEmail};message={result.Message}");
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        _db.InternalJobApplications.Add(new InternalJobApplication
        {
            JobPostingId = Job.Id,
            EmployeeUsername = Input.EmployeeName,
            EmployeeEmail = Input.EmployeeEmail,
            Notes = Input.Notes,
            ZendeskTicketId = result.TicketId,
            ZendeskTicketUrl = result.TicketUrl,
            SubmittedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(UserNameHelper.GetShortName(User), "ZendeskTicketCreated", "InternalJobApplication", $"job={Job.Id};employee={Input.EmployeeEmail};ticket={result.TicketId}");

        TempData["JobApplyMessage"] = "Application submitted to HR in Zendesk.";
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

        [MaxLength(80)]
        public string EmployeeId { get; set; } = string.Empty;

        [EmailAddress, MaxLength(200)]
        public string ManagerEmail { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }
}
