using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public sealed class BrowserSecurityOptions
{
    public const string SectionName = "BrowserSecurity";

    public bool EnableContentSecurityPolicy { get; set; } = true;

    public bool ReportOnly { get; set; } = true;

    public bool EnableInDevelopment { get; set; }

    [Required]
    [RegularExpression(@"^/[A-Za-z0-9/_-]+$")]
    public string ReportPath { get; set; } = "/security/csp-report";
}
