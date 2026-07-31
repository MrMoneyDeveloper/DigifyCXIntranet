namespace DigifyCXIntranet.Services;

public interface IEmailOutboxDispatcher
{
    Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken = default);
}
