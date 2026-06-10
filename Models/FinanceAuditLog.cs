using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class FinanceAuditLog
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Actor { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Entity { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(4000)]
    public string Detail { get; set; } = string.Empty;
}
