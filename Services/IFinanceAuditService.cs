namespace DigifyCXIntranet.Services;

public interface IFinanceAuditService
{
    Task WriteAsync(string actor, string action, string entity, string detail, CancellationToken cancellationToken = default);
}
