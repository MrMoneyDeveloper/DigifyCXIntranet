using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class HomePageOptions
{
    public const string SectionName = "HomePage";

    [Range(1, 365)]
    public int RecentJobDays { get; set; } = 30;
}
