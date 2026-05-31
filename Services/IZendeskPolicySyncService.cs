namespace DigifyCXIntranet.Services;

public interface IZendeskPolicySyncService
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}
