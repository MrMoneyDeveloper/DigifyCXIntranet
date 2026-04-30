using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public enum PolicyContentType
{
    Policy = 1,
    Procedure = 2
}

public class PolicyDocument
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public PolicyContentType ContentType { get; set; }

    [Required]
    [MaxLength(30)]
    public string VersionLabel { get; set; } = "v1.0";

    [Required]
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [MaxLength(120)]
    public string LastUpdatedBy { get; set; } = string.Empty;

    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
