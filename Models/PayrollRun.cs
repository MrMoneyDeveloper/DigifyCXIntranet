using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class PayrollRun
{
    public int Id { get; set; }

    [MaxLength(80)]
    public string? RunKey { get; set; }

    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime TriggeredUtc { get; set; } = DateTime.UtcNow;
    public int EmployeesCount { get; set; }
    public string EmailTo { get; set; } = string.Empty;
    public string AttachmentFileName { get; set; } = string.Empty;
    [MaxLength(400)]
    public string? ArtifactPath { get; set; }
    public bool SentSuccessfully { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
