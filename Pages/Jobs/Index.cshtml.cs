using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Pages.Jobs;

public class IndexModel : PageModel
{
    private static readonly string[] AllowedResumeExtensions = [".pdf", ".docx"];

    private readonly HrDbContext _db;
    private readonly IZendeskTicketService _zendeskTicketService;
    private readonly IFinanceAuditService _auditService;

    public IndexModel(
        HrDbContext db,
        IZendeskTicketService zendeskTicketService,
        IFinanceAuditService auditService)
    {
        _db = db;
        _zendeskTicketService = zendeskTicketService;
        _auditService = auditService;
    }

    [BindProperty]
    public ReferralInput Referral { get; set; } = new();

    [BindProperty]
    public IFormFile? ReferralResumeFile { get; set; }

    // TempData from Apply page (internal application result)
    [TempData] public string JobApplyMessage   { get; set; } = string.Empty;
    [TempData] public string JobApplyTicketId  { get; set; } = string.Empty;
    [TempData] public string JobApplyTicketUrl { get; set; } = string.Empty;

    // TempData for referral result (set and consumed on this page)
    [TempData] public string ReferralMessage   { get; set; } = string.Empty;
    [TempData] public string ReferralTicketId  { get; set; } = string.Empty;
    [TempData] public string ReferralTicketUrl { get; set; } = string.Empty;
    [TempData] public string ReferralError     { get; set; } = string.Empty;

    public List<JobPosting> Items { get; private set; } = new();

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostReferAsync()
    {
        await LoadAsync();

        if (!ModelState.IsValid)
            return Page();

        var job = await _db.JobPostings.FirstOrDefaultAsync(
            x => x.Id == Referral.JobPostingId && x.IsActive && !x.IsDeleted);
        if (job is null)
        {
            ModelState.AddModelError(string.Empty, "Selected job posting was not found.");
            return Page();
        }

        if (!job.IsExternalReferral)
        {
            ModelState.AddModelError(string.Empty, "External referrals are disabled for this job.");
            return Page();
        }

        // Validate optional resume file
        if (ReferralResumeFile is { Length: > 0 })
        {
            if (ReferralResumeFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(ReferralResumeFile), "Resume file must be 5 MB or less.");
                return Page();
            }
            var ext = Path.GetExtension(ReferralResumeFile.FileName).ToLowerInvariant();
            if (!AllowedResumeExtensions.Contains(ext))
            {
                ModelState.AddModelError(nameof(ReferralResumeFile), "Only PDF and DOCX files are allowed.");
                return Page();
            }
        }

        var referrerName  = UserNameHelper.GetShortName(User);
        var referrerEmail = User.FindFirstValue(ClaimTypes.Email) ?? $"{referrerName}@company.local";

        var ticket = await _zendeskTicketService.CreateReferralTicketAsync(
            job,
            referrerName,
            referrerEmail,
            Referral.CandidateName,
            Referral.CandidateEmail,
            Referral.CandidatePhone,
            Referral.Notes,
            ReferralResumeFile);

        var actor = referrerName;

        if (!ticket.Succeeded)
        {
            await _auditService.WriteAsync(actor, "ZendeskTicketFailed", "ReferralApplication",
                $"job={job.Id};candidate={Referral.CandidateEmail};error={ticket.Message}");
            // Surface error inline on the page (no redirect — keeps form state)
            ModelState.AddModelError(string.Empty, ticket.Message);
            return Page();
        }

        // Persist referral invite record
        var invite = new ReferralInvite
        {
            JobPostingId             = job.Id,
            ReferrerEmployeeUsername = referrerName,
            ReferrerEmployeeEmail    = referrerEmail,
            CandidateName            = Referral.CandidateName.Trim(),
            CandidateEmail           = Referral.CandidateEmail.Trim(),
            CandidatePhone           = Referral.CandidatePhone.Trim(),
            Notes                    = Referral.Notes.Trim(),
            Token                    = $"ref_{Guid.NewGuid():N}",
            ZendeskTicketId          = ticket.TicketId,
            CreatedUtc               = DateTime.UtcNow,
            ExpiresUtc               = DateTime.UtcNow.AddDays(14),
            IsConsumed               = true
        };
        _db.ReferralInvites.Add(invite);

        _db.ExternalApplications.Add(new ExternalApplication
        {
            JobPostingId     = job.Id,
            ReferralInvite   = invite,
            CandidateName    = Referral.CandidateName.Trim(),
            CandidateEmail   = Referral.CandidateEmail.Trim(),
            CandidatePhone   = Referral.CandidatePhone.Trim(),
            Notes            = Referral.Notes.Trim(),
            ZendeskTicketId  = ticket.TicketId,
            ZendeskTicketUrl = ticket.TicketUrl,
            SubmittedUtc     = DateTime.UtcNow,
            Status           = "Submitted"
        });

        await _db.SaveChangesAsync();
        await _auditService.WriteAsync(actor, "ZendeskTicketCreated", "ReferralApplication",
            $"job={job.Id};candidate={Referral.CandidateEmail};ticket={ticket.TicketId}");

        // Set TempData so the success banner renders after the redirect
        ReferralMessage   = $"Referral for {Referral.CandidateName.Trim()} submitted to HR successfully.";
        ReferralTicketId  = ticket.TicketId?.ToString() ?? string.Empty;
        ReferralTicketUrl = ticket.TicketUrl ?? string.Empty;

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await _db.JobPostings
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.ClosingDate)
            .ToListAsync();
    }

    public class ReferralInput
    {
        public int JobPostingId { get; set; }

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
