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
    public string EmployeeName { get; private set; } = string.Empty;
    public string EmployeeEmail { get; private set; } = string.Empty;

    [TempData] public string JobApplyMessage   { get; set; } = string.Empty;
    [TempData] public string JobApplyTicketId  { get; set; } = string.Empty;
    [TempData] public string JobApplyTicketUrl { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Job = await _db.JobPostings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted);
        if (Job is null) return NotFound();

        Input.JobPostingId = id;
        LoadEmployeeIdentity();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadEmployeeIdentity();
        Job = await _db.JobPostings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Input.JobPostingId && x.IsActive && !x.IsDeleted);
        if (Job is null) return NotFound();

        if (!ModelState.IsValid) return Page();

        try
        {
            await FileUploadValidator.ValidateAsync(ResumeFile, FileUploadPolicies.RequiredResume);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(ResumeFile), ex.Message);
            return Page();
        }

        // EmployeeId and ManagerEmail are not collected from the user —
        // pass empty strings so the service omits those Zendesk fields.
        // Ticket routing (form, group, tags) is unaffected.
        var result = await _zendeskTicketService.CreateInternalApplicationTicketAsync(
            Job,
            EmployeeName,
            EmployeeEmail,
            employeeId:   string.Empty,
            managerEmail: string.Empty,
            Input.Notes,
            ResumeFile);

        var actor = UserNameHelper.GetShortName(User);

        if (!result.Succeeded)
        {
            await _auditService.WriteAsync(actor, "ZendeskTicketFailed", "InternalJobApplication",
                $"job={Job.Id};employee={EmployeeEmail};error={result.Message}");
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        _db.InternalJobApplications.Add(new InternalJobApplication
        {
            JobPostingId     = Job.Id,
            EmployeeUsername = EmployeeName,
            EmployeeEmail    = EmployeeEmail,
            Notes            = Input.Notes,
            ZendeskTicketId  = result.TicketId,
            ZendeskTicketUrl = result.TicketUrl,
            SubmittedUtc     = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(actor, "ZendeskTicketCreated", "InternalJobApplication",
            $"job={Job.Id};employee={EmployeeEmail};ticket={result.TicketId}");

        JobApplyMessage   = $"Application submitted successfully for \u201c{Job.Title}\u201d.";
        JobApplyTicketId  = result.TicketId?.ToString() ?? string.Empty;
        JobApplyTicketUrl = result.TicketUrl ?? string.Empty;

        return RedirectToPage("/Jobs/Index");
    }

    private void LoadEmployeeIdentity()
    {
        (EmployeeName, EmployeeEmail) = ResolveEmployeeIdentity(User);
    }

    internal static (string Name, string Email) ResolveEmployeeIdentity(ClaimsPrincipal user)
    {
        var name = UserNameHelper.GetShortName(user);
        var email = user.FindFirstValue(ClaimTypes.Email);
        return (name, string.IsNullOrWhiteSpace(email) ? $"{name}@company.local" : email);
    }

    public class InputModel
    {
        public int JobPostingId { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }
}
