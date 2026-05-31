namespace DigifyCXIntranet.Options;

public class TechNewsOptions
{
    public const string SectionName = "TechNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0";
    public int TopCount { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 12;
}
