using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class CanteenBatchRun
{
    public int Id { get; set; }

    [MaxLength(80)]
    public string? RunKey { get; set; }

    public MealSlot MealSlot { get; set; }
    public DateTime CutoffLocalTime { get; set; }
    public DateTime TriggeredUtc { get; set; } = DateTime.UtcNow;
    public int OrdersCount { get; set; }
    public string EmailTo { get; set; } = string.Empty;
    public string AttachmentFileName { get; set; } = string.Empty;
    [MaxLength(400)]
    public string? ArtifactPath { get; set; }
    public bool SentSuccessfully { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public List<CanteenOrder> Orders { get; set; } = new();
}
