using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class TechNewsOptions
{
    public const string SectionName = "TechNews";

    [Url]
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0";

    [Range(3, 30)]
    public int TopCount { get; set; } = 10;

    [Range(5, 60)]
    public int TimeoutSeconds { get; set; } = 12;

    [Range(1, 8)]
    public int MaxConcurrentRequests { get; set; } = 4;
}
