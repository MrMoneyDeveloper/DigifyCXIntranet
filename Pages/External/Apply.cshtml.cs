using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.External;

public class ApplyModel : PageModel
{
    private readonly HrDbContext _db;
    private readonly IZendeskTicketService _zendeskTicketService;
    private readonly IFinanceAuditService _auditService;
    private readonly IAuditService _generalAuditService;
    private readonly IIdentifierRateLimiter _identifierRateLimiter;

    public ApplyModel(
        HrDbContext db,
        IZendeskTicketService zendeskTicketService,
        IFinanceAuditService auditService,
        IAuditService generalAuditService,
        IIdentifierRateLimiter identifierRateLimiter)
    {
        _db = db;
        _zendeskTicketService = zendeskTicketService;
        _auditService = auditService;
        _generalAuditService = generalAuditService;
        _identifierRateLimiter = identifierRateLimiter;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? ResumeFile { get; set; }

    public JobPosting? Job { get; private set; }
    public bool TokenValid { get; private set; }

    public async Task OnGetAsync(string token)
    {
        await LoadTokenContextAsync(token);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTokenContextAsync(Input.Token);
        if (!TokenValid || Job is null || !ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await FileUploadValidator.ValidateAsync(ResumeFile, FileUploadPolicies.RequiredResume);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(ResumeFile), ex.Message);
            return Page();
        }

        if (!_identifierRateLimiter.TryAcquire("external-application", Input.Token))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ModelState.AddModelError(string.Empty, "Too many requests. Please wait and try again.");
            await _generalAuditService.WriteAsync(
                "external",
                "ExternalApplicationRateLimited",
                "ExternalApplication",
                "identifier-limit-rejected",
                succeeded: false,
                errorCode: "RateLimited",
                httpContext: HttpContext);
            return Page();
        }

        var invite = await _db.ReferralInvites
            .FirstAsync(x => x.Token == Input.Token);

        var ticket = await _zendeskTicketService.CreateReferralTicketAsync(
            Job,
            invite.ReferrerEmployeeUsername,
            invite.ReferrerEmployeeEmail,
            Input.CandidateName,
            Input.CandidateEmail,
            Input.CandidatePhone,
            Input.Notes,
            ResumeFile);

        if (!ticket.Succeeded)
        {
            await _auditService.WriteAsync("external", "ZendeskTicketFailed", "ExternalApplication", $"job={Job.Id};candidate={Input.CandidateEmail};message={ticket.Message}");
            ModelState.AddModelError(string.Empty, ticket.Message);
            return Page();
        }

        _db.ExternalApplications.Add(new ExternalApplication
        {
            JobPostingId = Job.Id,
            ReferralInviteId = invite.Id,
            CandidateName = Input.CandidateName,
            CandidateEmail = Input.CandidateEmail,
            CandidatePhone = Input.CandidatePhone,
            Notes = Input.Notes,
            ZendeskTicketId = ticket.TicketId,
            ZendeskTicketUrl = ticket.TicketUrl,
            SubmittedUtc = DateTime.UtcNow,
            Status = "Submitted"
        });
        invite.IsConsumed = true;
        invite.ZendeskTicketId = ticket.TicketId;

        await _db.SaveChangesAsync();
        await _auditService.WriteAsync("external", "ZendeskTicketCreated", "ExternalApplication", $"job={Job.Id};candidate={Input.CandidateEmail};ticket={ticket.TicketId}");
        await _generalAuditService.WriteAsync(
            "external",
            "ExternalApplicationSubmitted",
            "ExternalApplication",
            $"job={Job.Id};ticket={ticket.TicketId}",
            entityId: ticket.TicketId?.ToString() ?? string.Empty,
            httpContext: HttpContext);
        TempData["ExternalApplyDone"] = "Application submitted successfully.";
        return RedirectToPage("/External/Apply", new { token = Input.Token });
    }

    private async Task LoadTokenContextAsync(string token)
    {
        Input.Token = token;
        TokenValid = false;

        var invite = await _db.ReferralInvites
            .AsNoTracking()
            .Include(x => x.JobPosting)
            .FirstOrDefaultAsync(x => x.Token == token);
        if (invite is null || invite.ExpiresUtc < DateTime.UtcNow || invite.IsConsumed)
        {
            return;
        }

        Job = invite.JobPosting;
        TokenValid = Job is not null && Job.IsActive && !Job.IsDeleted;
    }

    public class InputModel
    {
        public string Token { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string CandidateName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string CandidateEmail { get; set; } = string.Empty;

        [Phone, MaxLength(60)]
        public string CandidatePhone { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }
}
