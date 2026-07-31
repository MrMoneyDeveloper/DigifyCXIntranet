using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class BackgroundJobRun
{
    public long Id { get; set; }

    [Required, MaxLength(120)]
    public string JobName { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Status { get; set; } = "Started";

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public DateTime? NextFireUtc { get; set; }
    public int Attempt { get; set; } = 1;
    public int ItemsProcessed { get; set; }

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}
