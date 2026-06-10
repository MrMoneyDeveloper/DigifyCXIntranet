using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class InternalJobApplication
{
    public int Id { get; set; }
    public int JobPostingId { get; set; }
    public JobPosting? JobPosting { get; set; }

    [Required]
    [MaxLength(120)]
    public string EmployeeUsername { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string EmployeeEmail { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    public long? ZendeskTicketId { get; set; }

    [MaxLength(500)]
    public string ZendeskTicketUrl { get; set; } = string.Empty;

    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
}
