namespace DigifyCXIntranet.Services;

public interface IAuditService
{
    Task WriteAsync(
        string actor,
        string action,
        string entity,
        string detail,
        bool succeeded = true,
        string entityId = "",
        string errorCode = "",
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default);
}
