using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class Announcement
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(250)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;

    public DateTime PublishDateUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ExpirationDate { get; set; }

    public bool IsPinned { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(120)]
    public string LastUpdatedBy { get; set; } = string.Empty;
}
