using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Models;

public class AuditLog
{
    public long Id { get; set; }

    [Required, MaxLength(120)]
    public string Actor { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Entity { get; set; } = string.Empty;

    [MaxLength(120)]
    public string EntityId { get; set; } = string.Empty;

    public bool Succeeded { get; set; } = true;

    [MaxLength(80)]
    public string ErrorCode { get; set; } = string.Empty;

    [MaxLength(80)]
    public string CorrelationId { get; set; } = string.Empty;

    [MaxLength(80)]
    public string RemoteIp { get; set; } = string.Empty;

    [MaxLength(512)]
    public string UserAgent { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Route { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(4000)]
    public string Detail { get; set; } = string.Empty;
}
