using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.External;

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

        var ext = Path.GetExtension(ResumeFile.FileName).ToLowerInvariant();
        if (!AllowedResumeExtensions.Contains(ext))
        {
            ModelState.AddModelError(nameof(ResumeFile), "Only PDF and DOCX files are allowed.");
            return Page();
        }

        var invite = await _db.ReferralInvites
            .FirstAsync(x => x.Token == Input.Token);

        await using var ms = new MemoryStream();
        await ResumeFile.CopyToAsync(ms);

        await _emailSender.SendAsync(new EmailMessage
        {
            To = _inboxes.HrReferralInbox,
            Subject = $"External Referral Application: {Job.Title}",
            BodyText = $"Candidate: {Input.CandidateName}\nEmail: {Input.CandidateEmail}\nReferred by: {invite.ReferrerEmployeeUsername}\nNotes: {Input.Notes}",
            Attachments = new List<EmailAttachment>
            {
                new()
                {
                    FileName = ResumeFile.FileName,
                    ContentType = ResumeFile.ContentType,
                    Bytes = ms.ToArray()
                }
            }
        });

        _db.ExternalApplications.Add(new ExternalApplication
        {
            JobPostingId = Job.Id,
            ReferralInviteId = invite.Id,
            CandidateName = Input.CandidateName,
            CandidateEmail = Input.CandidateEmail,
            Notes = Input.Notes,
            SubmittedUtc = DateTime.UtcNow,
            Status = "Submitted"
        });
        invite.IsConsumed = true;

        await _db.SaveChangesAsync();
        TempData["ExternalApplyDone"] = "Application submitted successfully.";
        return RedirectToPage("/External/Apply", new { token = Input.Token });
    }

    private async Task LoadTokenContextAsync(string token)
    {
        Input.Token = token;
        TokenValid = false;

        var invite = await _db.ReferralInvites
            .Include(x => x.JobPosting)
            .FirstOrDefaultAsync(x => x.Token == token);
        if (invite is null || invite.ExpiresUtc < DateTime.UtcNow || invite.IsConsumed)
        {
            return;
        }

        Job = invite.JobPosting;
        TokenValid = Job is not null;
    }

    public class InputModel
    {
        public string Token { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string CandidateName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string CandidateEmail { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }
}
