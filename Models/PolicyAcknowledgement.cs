using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class PolicyAcknowledgement
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string EmployeeDomainName { get; set; } = string.Empty;

    public long PolicyArticleId { get; set; }

    [Required]
    [MaxLength(60)]
    public string PolicyVersion { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
