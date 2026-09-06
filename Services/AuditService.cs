using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;

namespace DigifyCXIntranet.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task WriteAsync(
        string actor,
        string action,
        string entity,
        string detail,
        bool succeeded = true,
        string entityId = "",
        string errorCode = "",
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default)
    {
        var request = httpContext?.Request;
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Actor = Clean(actor, 120, "system"),
                Action = Clean(action, 120, "Unknown"),
                Entity = Clean(entity, 120, "Unknown"),
                EntityId = Clean(entityId, 120),
                Succeeded = succeeded,
                ErrorCode = Clean(errorCode, 80),
                CorrelationId = Clean(httpContext?.TraceIdentifier, 80),
                RemoteIp = Clean(GetRemoteIp(httpContext), 80),
                UserAgent = Clean(request?.Headers.UserAgent.ToString(), 512),
                Route = Clean(request?.Path.Value, 256),
                TimestampUtc = DateTime.UtcNow,
                Detail = Clean(AuditRedactor.Redact(detail), 4000)
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            _logger.LogError(ex, "Audit write failed for {Action} {Entity}. Request flow will continue.", action, entity);
        }
    }

    private static string GetRemoteIp(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return string.Empty;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private static string Clean(string? value, int maxLength, string fallback = "")
    {
        var result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return result.Length <= maxLength ? result : result[..maxLength];
    }
}
