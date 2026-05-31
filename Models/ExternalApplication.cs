using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class ExternalApplication
{
    public int Id { get; set; }

    public int JobPostingId { get; set; }
    public JobPosting? JobPosting { get; set; }

    public int ReferralInviteId { get; set; }
    public ReferralInvite? ReferralInvite { get; set; }

    [Required]
    [MaxLength(150)]
    public string CandidateName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CandidateEmail { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(80)]
    public string Status { get; set; } = "Submitted";
}
