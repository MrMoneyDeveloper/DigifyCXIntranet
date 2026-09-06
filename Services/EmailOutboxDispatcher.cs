using System.Net;
using System.Net.Mail;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class EmailOutboxDispatcher : IEmailOutboxDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly SmtpOptions _smtp;
    private readonly ILogger<EmailOutboxDispatcher> _logger;

    public EmailOutboxDispatcher(
        ApplicationDbContext db,
        IOptions<SmtpOptions> smtpOptions,
        ILogger<EmailOutboxDispatcher> logger)
    {
        _db = db;
        _smtp = smtpOptions.Value;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var messageIds = await _db.EmailOutboxMessages
            .AsNoTracking()
            .Where(x => x.Status == EmailOutboxStatus.Pending || x.Status == EmailOutboxStatus.Processing)
            .OrderBy(x => x.CreatedUtc)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(Math.Clamp(batchSize, 1, 100))
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var messageId in messageIds)
        {
            var message = await _db.EmailOutboxMessages
                .Include(x => x.Attachments)
                .SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken);
            if (message is null)
            {
                continue;
            }

            message.Status = EmailOutboxStatus.Processing;
            message.Attempts++;
            message.LastAttemptUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                await SendSmtpAsync(message, cancellationToken);
                message.Status = EmailOutboxStatus.Sent;
                message.SentUtc = DateTime.UtcNow;
                message.LastError = string.Empty;
                foreach (var attachment in message.Attachments)
                {
                    attachment.Bytes = Array.Empty<byte>();
                }
                sent++;
            }
            catch (Exception ex)
            {
                message.Status = message.Attempts >= 5 ? EmailOutboxStatus.Failed : EmailOutboxStatus.Pending;
                message.LastError = ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000];
                _logger.LogError(ex, "Email outbox dispatch failed for message {MessageId}", message.Id);
            }

            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }

        return sent;
    }

    private async Task SendSmtpAsync(EmailOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!_smtp.Enabled || string.IsNullOrWhiteSpace(_smtp.Host))
        {
            throw new InvalidOperationException("SMTP is disabled or not configured.");
        }

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
            mail.Attachments.Add(new Attachment(new MemoryStream(attachment.Bytes), attachment.FileName, attachment.ContentType));
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
}
