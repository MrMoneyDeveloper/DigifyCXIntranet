using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class ReferralInvite
{
    public int Id { get; set; }
    public int JobPostingId { get; set; }
    public JobPosting? JobPosting { get; set; }

    [Required]
    [MaxLength(120)]
    public string ReferrerEmployeeUsername { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ReferrerEmployeeEmail { get; set; } = string.Empty;

    [MaxLength(150)]
    public string CandidateName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CandidateEmail { get; set; } = string.Empty;

    [MaxLength(60)]
    public string CandidatePhone { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    public long? ZendeskTicketId { get; set; }

    [Required]
    [MaxLength(120)]
    public string Token { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public bool IsConsumed { get; set; }
}
