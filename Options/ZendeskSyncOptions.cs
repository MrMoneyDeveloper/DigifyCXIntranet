namespace DigifyCXIntranet.Options;

public class ZendeskSyncOptions
{
    public const string SectionName = "ZendeskSync";

    public string BaseUrl { get; set; } = string.Empty;
    public string Locale { get; set; } = "en-us";
    public bool UseApiToken { get; set; } = false;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;
}
