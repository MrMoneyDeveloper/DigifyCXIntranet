using System.Net;
using System.Net.Mail;
using System.Text.Json;
using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class SmtpOrOutboxEmailSender : IEmailSender
{
    private readonly SmtpOptions _smtp;
    private readonly OutboxOptions _outbox;
    private readonly ILogger<SmtpOrOutboxEmailSender> _logger;
    private readonly IWebHostEnvironment _env;

    public SmtpOrOutboxEmailSender(
        IOptions<SmtpOptions> smtpOptions,
        IOptions<OutboxOptions> outboxOptions,
        IWebHostEnvironment env,
        ILogger<SmtpOrOutboxEmailSender> logger)
    {
        _smtp = smtpOptions.Value;
        _outbox = outboxOptions.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (_smtp.Enabled && !string.IsNullOrWhiteSpace(_smtp.Host))
        {
            try
            {
                await SendSmtpAsync(message, cancellationToken);
                return new EmailSendResult
                {
                    DeliveredViaSmtp = true,
                    DeliveredViaOutbox = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP send failed, falling back to outbox for {To}", message.To);
            }
        }

        var outboxPath = await WriteOutboxAsync(message, cancellationToken);
        return new EmailSendResult
        {
            DeliveredViaSmtp = false,
            DeliveredViaOutbox = !string.IsNullOrWhiteSpace(outboxPath),
            ArtifactPath = outboxPath
        };
    }

    private async Task SendSmtpAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_smtp.FromAddress, _smtp.FromName),
            Subject = message.Subject,
            Body = message.BodyText,
            IsBodyHtml = false
        };
        mail.To.Add(message.To);

        foreach (var attachment in message.Attachments)
        {
            var ms = new MemoryStream(attachment.Bytes);
            mail.Attachments.Add(new Attachment(ms, attachment.FileName, attachment.ContentType));
        }

        using var client = new SmtpClient(_smtp.Host, _smtp.Port)
        {
            EnableSsl = _smtp.UseSsl
        };
        if (!string.IsNullOrWhiteSpace(_smtp.Username))
        {
            client.Credentials = new NetworkCredential(_smtp.Username, _smtp.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail, cancellationToken);
    }

    private async Task<string> WriteOutboxAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!_outbox.Enabled)
        {
            _logger.LogInformation("Outbox disabled. Email to {To} logged only. Subject: {Subject}", message.To, message.Subject);
            return string.Empty;
        }

        var folderPath = Path.IsPathRooted(_outbox.FolderPath)
            ? _outbox.FolderPath
            : Path.Combine(_env.ContentRootPath, _outbox.FolderPath);
        Directory.CreateDirectory(folderPath);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var emailFolder = Path.Combine(folderPath, $"{stamp}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(emailFolder);

        string firstAttachmentPath = string.Empty;
        for (var i = 0; i < message.Attachments.Count; i++)
        {
            var attachment = message.Attachments[i];
            var safeName = string.IsNullOrWhiteSpace(attachment.FileName)
                ? $"attachment_{i + 1}.bin"
                : attachment.FileName;
            var attachmentPath = Path.Combine(emailFolder, safeName);
            await File.WriteAllBytesAsync(attachmentPath, attachment.Bytes, cancellationToken);
            if (string.IsNullOrWhiteSpace(firstAttachmentPath))
            {
                firstAttachmentPath = attachmentPath;
            }
        }

        var meta = new
        {
            message.To,
            message.Subject,
            message.BodyText,
            GeneratedUtc = DateTime.UtcNow,
            Attachments = message.Attachments.Select(x => new { x.FileName, x.ContentType }).ToList()
        };
        var metaPath = Path.Combine(emailFolder, "email.json");
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metaPath, json, cancellationToken);

        _logger.LogInformation("Email written to outbox at {Path}", emailFolder);
        return string.IsNullOrWhiteSpace(firstAttachmentPath) ? metaPath : firstAttachmentPath;
    }
}
