using System.ComponentModel.DataAnnotations;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Pages.Jobs;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly RoutingInboxesOptions _inboxes;

    public IndexModel(
        ApplicationDbContext db,
        IEmailSender emailSender,
        IOptions<RoutingInboxesOptions> inboxOptions)
    {
        _db = db;
        _emailSender = emailSender;
        _inboxes = inboxOptions.Value;
    }

    [BindProperty]
    public ReferralInput Referral { get; set; } = new();

    [TempData]
    public string FeedbackMessage { get; set; } = string.Empty;

    [TempData]
    public string JobApplyMessage { get; set; } = string.Empty;

    public List<JobPosting> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostReferAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var job = await _db.JobPostings.FirstOrDefaultAsync(x => x.Id == Referral.JobPostingId && x.IsActive);
        if (job is null)
        {
            ModelState.AddModelError(string.Empty, "Selected job posting was not found.");
            await LoadAsync();
            return Page();
        }

        if (!job.IsExternalReferral)
        {
            ModelState.AddModelError(string.Empty, "External referrals are disabled for this job.");
            await LoadAsync();
            return Page();
        }

        var token = $"ref_{Guid.NewGuid():N}";
        var invite = new ReferralInvite
        {
            JobPostingId = job.Id,
            ReferrerEmployeeUsername = UserNameHelper.GetShortName(User),
            CandidateEmail = Referral.CandidateEmail.Trim(),
            Token = token,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddDays(14)
        };
        _db.ReferralInvites.Add(invite);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var link = $"{baseUrl}/External/Apply?token={token}";

        await _emailSender.SendAsync(new EmailMessage
        {
            To = Referral.CandidateEmail.Trim(),
            Subject = $"Job referral from DigifyCX: {job.Title}",
            BodyText = $"You were referred for '{job.Title}'. Apply securely here: {link}\nThis link expires in 14 days."
        });

        FeedbackMessage = "Referral invitation sent.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Items = await _db.JobPostings
            .Where(x => x.IsActive)
            .OrderBy(x => x.ClosingDate)
            .ToListAsync();
    }

    public class ReferralInput
    {
        public int JobPostingId { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string CandidateEmail { get; set; } = string.Empty;
    }
}
