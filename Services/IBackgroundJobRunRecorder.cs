namespace DigifyCXIntranet.Services;

public interface IBackgroundJobRunRecorder
{
    Task<long> StartedAsync(string jobName, DateTime? nextFireUtc, int attempt, CancellationToken cancellationToken);
    Task CompletedAsync(long runId, int itemsProcessed, string message, CancellationToken cancellationToken);
    Task FailedAsync(long runId, string message, CancellationToken cancellationToken);
}
