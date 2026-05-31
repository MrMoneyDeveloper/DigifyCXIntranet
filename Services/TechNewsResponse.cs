namespace DigifyCXIntranet.Services;

public class TechNewsResponse
{
    public DateTimeOffset? LastUpdatedUtc { get; set; }
    public List<TechNewsFeedItem> Items { get; set; } = new();
}
