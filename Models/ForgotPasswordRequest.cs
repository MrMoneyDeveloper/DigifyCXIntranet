namespace DigifyCXIntranet.Models;

/// <summary>
/// Persisted record of every forgotten-password Zendesk ticket submission.
/// Kept for audit purposes so IT can cross-reference ticket IDs.
/// </summary>
public class ForgotPasswordRequest
{
    public int     Id              { get; set; }

    /// <summary>Full name the user entered on the form.</summary>
    public string  FullName        { get; set; } = string.Empty;

    /// <summary>Auto-generated description sent to Zendesk.</summary>
    public string  Description     { get; set; } = string.Empty;

    /// <summary>Remote IP address captured server-side.</summary>
    public string  IpAddress       { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the submission.</summary>
    public DateTime SubmittedUtc   { get; set; }

    /// <summary>Zendesk ticket ID — null if the API call failed.</summary>
    public long?   ZendeskTicketId { get; set; }

    /// <summary>Zendesk ticket URL — empty if the API call failed.</summary>
    public string  ZendeskTicketUrl { get; set; } = string.Empty;

    /// <summary>Whether the Zendesk ticket was created successfully.</summary>
    public bool    Succeeded       { get; set; }
}
