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

    [BindProperty]
    public IFormFile? ResumeFile { get; set; }

    public JobPosting? Job { get; private set; }

    [TempData] public string JobApplyMessage   { get; set; } = string.Empty;
    [TempData] public string JobApplyTicketId  { get; set; } = string.Empty;
    [TempData] public string JobApplyTicketUrl { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Job = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted);
        if (Job is null) return NotFound();

        Input.JobPostingId  = id;
        Input.EmployeeName  = UserNameHelper.GetShortName(User);
        Input.EmployeeEmail = User.FindFirstValue(ClaimTypes.Email) ?? $"{Input.EmployeeName}@company.local";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Job = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == Input.JobPostingId && x.IsActive && !x.IsDeleted);
        if (Job is null) return NotFound();

        if (!ModelState.IsValid) return Page();

        if (ResumeFile is null || ResumeFile.Length <= 0)
        {
            ModelState.AddModelError(nameof(ResumeFile), "Resume file is required.");
            return Page();
        }
        if (ResumeFile.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(ResumeFile), "Resume file must be 5 MB or less.");
            return Page();
        }
        var ext = Path.GetExtension(ResumeFile.FileName).ToLowerInvariant();
        if (!AllowedResumeExtensions.Contains(ext))
        {
            ModelState.AddModelError(nameof(ResumeFile), "Only PDF and DOCX files are allowed.");
            return Page();
        }

        // EmployeeId and ManagerEmail are not collected from the user —
        // pass empty strings so the service omits those Zendesk fields.
        // Ticket routing (form, group, tags) is unaffected.
        var result = await _zendeskTicketService.CreateInternalApplicationTicketAsync(
            Job,
            Input.EmployeeName,
            Input.EmployeeEmail,
            employeeId:   string.Empty,
            managerEmail: string.Empty,
            Input.Notes,
            ResumeFile);

        var actor = UserNameHelper.GetShortName(User);

        if (!result.Succeeded)
        {
            await _auditService.WriteAsync(actor, "ZendeskTicketFailed", "InternalJobApplication",
                $"job={Job.Id};employee={Input.EmployeeEmail};error={result.Message}");
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        _db.InternalJobApplications.Add(new InternalJobApplication
        {
            JobPostingId     = Job.Id,
            EmployeeUsername = Input.EmployeeName,
            EmployeeEmail    = Input.EmployeeEmail,
            Notes            = Input.Notes,
            ZendeskTicketId  = result.TicketId,
            ZendeskTicketUrl = result.TicketUrl,
            SubmittedUtc     = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(actor, "ZendeskTicketCreated", "InternalJobApplication",
            $"job={Job.Id};employee={Input.EmployeeEmail};ticket={result.TicketId}");

        JobApplyMessage   = $"Application submitted successfully for \u201c{Job.Title}\u201d.";
        JobApplyTicketId  = result.TicketId?.ToString() ?? string.Empty;
        JobApplyTicketUrl = result.TicketUrl ?? string.Empty;

        return RedirectToPage("/Jobs/Index");
    }

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
