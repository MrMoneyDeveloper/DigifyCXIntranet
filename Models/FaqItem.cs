using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class FaqItem
{
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(250)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [MaxLength(5000)]
    public string Answer { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
