using System.Text.Json;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class SmtpOrOutboxEmailSender : IEmailSender
{
    private readonly OutboxOptions _outbox;
    private readonly ILogger<SmtpOrOutboxEmailSender> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;

    public SmtpOrOutboxEmailSender(
        ApplicationDbContext db,
        IOptions<OutboxOptions> outboxOptions,
        IWebHostEnvironment env,
        ILogger<SmtpOrOutboxEmailSender> logger)
    {
        _db = db;
        _outbox = outboxOptions.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var queued = new EmailOutboxMessage
        {
            To = message.To.Trim(),
            Subject = message.Subject.Trim(),
            BodyText = message.BodyText,
            Status = EmailOutboxStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            Attachments = message.Attachments.Select((attachment, index) => new EmailOutboxAttachment
            {
                FileName = SafeFileNames.Normalize(attachment.FileName, $"attachment_{index + 1}.bin"),
                ContentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType.Trim(),
                Bytes = attachment.Bytes
            }).ToList()
        };

        _db.EmailOutboxMessages.Add(queued);
        await _db.SaveChangesAsync(cancellationToken);

        string artifactPath = $"email-outbox:{queued.Id}";
        if (_outbox.Enabled)
        {
            try
            {
                var diskPath = await WriteOutboxAsync(message, cancellationToken);
                if (!string.IsNullOrWhiteSpace(diskPath))
                {
                    artifactPath = $"{artifactPath};{diskPath}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email was queued in the database but filesystem outbox write failed for {To}", message.To);
            }
        }

        _logger.LogInformation("Email queued in outbox message {MessageId} for {To}", queued.Id, queued.To);
        return new EmailSendResult
        {
            DeliveredViaSmtp = false,
            DeliveredViaOutbox = true,
            ArtifactPath = artifactPath
        };
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
            var safeName = SafeFileNames.Normalize(attachment.FileName, $"attachment_{i + 1}.bin");
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
