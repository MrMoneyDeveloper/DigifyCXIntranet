using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;

namespace DigifyCXIntranet.Services;

public class FinanceAuditService : IFinanceAuditService
{
    private readonly CanteenDbContext _db;

    public FinanceAuditService(CanteenDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(string actor, string action, string entity, string detail, CancellationToken cancellationToken = default)
    {
        _db.FinanceAuditLogs.Add(new FinanceAuditLog
        {
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(),
            Action = action.Trim(),
            Entity = entity.Trim(),
            TimestampUtc = DateTime.UtcNow,
            Detail = detail.Length > 4000 ? detail[..4000] : detail
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
