using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.Extensions.Options;
using Quartz;

namespace DigifyCXIntranet.BackgroundJobs;

[DisallowConcurrentExecution]
public class TechNewsRefreshJob : IJob
{
    private readonly ITechNewsCacheService _cacheService;
    private readonly IBackgroundJobRunRecorder _runs;
    private readonly JobSchedulingOptions _options;

    public TechNewsRefreshJob(
        ITechNewsCacheService cacheService,
        IBackgroundJobRunRecorder runs,
        IOptions<JobSchedulingOptions> options)
    {
        _cacheService = cacheService;
        _runs = runs;
        _options = options.Value;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var runId = await _runs.StartedAsync(JobNames.TechNewsRefresh, context.NextFireTimeUtc?.UtcDateTime, context.RefireCount + 1, context.CancellationToken);
        try
        {
            await _cacheService.RefreshAsync(context.CancellationToken);
            await _runs.CompletedAsync(runId, _cacheService.GetCurrentItems().Count, "Tech news refresh completed.", context.CancellationToken);
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
