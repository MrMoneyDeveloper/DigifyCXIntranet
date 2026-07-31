using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class EmailOutboxMessage
{
    public long Id { get; set; }

    [Required, MaxLength(320)]
    public string To { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string BodyText { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Status { get; set; } = EmailOutboxStatus.Pending;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public int Attempts { get; set; }

    [MaxLength(1000)]
    public string LastError { get; set; } = string.Empty;

    public List<EmailOutboxAttachment> Attachments { get; set; } = new();
}

public class EmailOutboxAttachment
{
    public long Id { get; set; }
    public long EmailOutboxMessageId { get; set; }
    public EmailOutboxMessage Message { get; set; } = null!;

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    public byte[] Bytes { get; set; } = Array.Empty<byte>();
}

public static class EmailOutboxStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}
