namespace DigifyCXIntranet.Services;

public class TechNewsFeedItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string By { get; set; } = string.Empty;
    public long TimeUnix { get; set; }
}
