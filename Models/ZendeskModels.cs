namespace DigifyCXIntranet.Models;

public class ZendeskArticleResponse
{
    public List<ZendeskApiArticle> Articles { get; set; } = new();
}

public class ZendeskApiArticle
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool Draft { get; set; }
} 