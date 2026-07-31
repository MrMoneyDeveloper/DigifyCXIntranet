namespace DigifyCXIntranet.Services;

public interface IUserRegistrySyncService
{
    Task<UserRegistrySyncResult> SyncAsync(CancellationToken cancellationToken = default);
}

public record UserRegistrySyncResult(int Created, int Updated, int Skipped);
