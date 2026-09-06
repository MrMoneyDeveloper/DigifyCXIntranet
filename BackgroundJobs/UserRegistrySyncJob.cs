using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.Extensions.Options;
using Quartz;

namespace DigifyCXIntranet.BackgroundJobs;

[DisallowConcurrentExecution]
public class UserRegistrySyncJob : IJob
{
    private readonly IUserRegistrySyncService _syncService;
    private readonly IBackgroundJobRunRecorder _runs;
    private readonly JobSchedulingOptions _options;

    public UserRegistrySyncJob(
        IUserRegistrySyncService syncService,
        IBackgroundJobRunRecorder runs,
        IOptions<JobSchedulingOptions> options)
    {
        _syncService = syncService;
        _runs = runs;
        _options = options.Value;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var runId = await _runs.StartedAsync(JobNames.UserRegistrySync, context.NextFireTimeUtc?.UtcDateTime, context.RefireCount + 1, context.CancellationToken);
        try
        {
            var result = await _syncService.SyncAsync(context.CancellationToken);
            await _runs.CompletedAsync(
                runId,
                result.Created + result.Updated + result.Deleted,
                $"Created={result.Created};Updated={result.Updated};Deleted={result.Deleted};Skipped={result.Skipped}",
                context.CancellationToken);
        }
        catch (Exception ex) when (context.RefireCount < _options.RetryCount)
        {
            await _runs.FailedAsync(runId, ex.Message, CancellationToken.None);
            await Task.Delay(TimeSpan.FromMinutes(_options.RetryDelayMinutes), context.CancellationToken);
            throw new JobExecutionException(ex, refireImmediately: true);
        }
        catch (Exception ex)
        {
            await _runs.FailedAsync(runId, ex.Message, CancellationToken.None);
            throw;
        }
    }
}
