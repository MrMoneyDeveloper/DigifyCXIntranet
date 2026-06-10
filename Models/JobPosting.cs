using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class JobPosting
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ApplicationRoute { get; set; } = "HR Inbox";

    public DateOnly ClosingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(14));

    public bool IsExternalReferral { get; set; }

    public bool UseVisualAd { get; set; } = true;

    [MaxLength(180)]
    public string AdHeadline { get; set; } = string.Empty;

    [MaxLength(450)]
    public string AdSubHeadline { get; set; } = string.Empty;

    [MaxLength(250)]
    public string AdBackgroundImagePath { get; set; } = "/images/job-ad-a4-template.svg";

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    [MaxLength(120)]
    public string LastUpdatedBy { get; set; } = string.Empty;

    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDateUtc { get; set; } = DateTime.UtcNow;
}
