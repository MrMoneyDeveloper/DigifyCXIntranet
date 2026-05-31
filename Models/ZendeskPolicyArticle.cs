using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class ZendeskPolicyArticle
{
    public int Id { get; set; }

    public long ZendeskArticleId { get; set; }

    [Required]
    [MaxLength(220)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(400)]
    public string HtmlUrl { get; set; } = string.Empty;

    [MaxLength(60)]
    public string VersionLabel { get; set; } = "v1.0";

    [MaxLength(12000)]
    public string Body { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}
