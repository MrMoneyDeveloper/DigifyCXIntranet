namespace DigifyCXIntranet.Options;

/// <summary>
/// Configuration for the inbound Zendesk webhook that triggers password resets.
/// Bind from appsettings.json section "ZendeskWebhook".
/// On the live server, override Secret via an environment variable:
///   ZendeskWebhook__Secret=<your-secret>
/// Never commit the real secret to source control.
/// </summary>
public sealed class ZendeskWebhookOptions
{
    /// <summary>
    /// The shared secret that Zendesk sends in the X-Zendesk-Webhook-Secret header.
    /// Must match what is configured in the Zendesk webhook settings.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}
