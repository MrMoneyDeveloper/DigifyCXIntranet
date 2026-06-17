namespace DigifyCXIntranet.Models;

public class AccountActivationLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PersonalEmail { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ActivatedUtc { get; set; }
}
