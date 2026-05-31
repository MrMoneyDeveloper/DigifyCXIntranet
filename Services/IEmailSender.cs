namespace DigifyCXIntranet.Services;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public class EmailSendResult
{
    public bool DeliveredViaSmtp { get; set; }
    public bool DeliveredViaOutbox { get; set; }
    public string ArtifactPath { get; set; } = string.Empty;
}
