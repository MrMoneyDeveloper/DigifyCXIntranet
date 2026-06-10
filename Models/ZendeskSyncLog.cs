using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class ZendeskSyncLog
{
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Operation { get; set; } = string.Empty;

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedUtc { get; set; }

    public bool Succeeded { get; set; }

    public int ItemsProcessed { get; set; }

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}
