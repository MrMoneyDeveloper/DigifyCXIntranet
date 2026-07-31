using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.Extensions.Options;
using Quartz;

namespace DigifyCXIntranet.BackgroundJobs;

[DisallowConcurrentExecution]
public class LunchCanteenBatchJob : IJob
{
    private readonly ICanteenBatchService _canteenBatchService;
    private readonly IBackgroundJobRunRecorder _runs;
    private readonly JobSchedulingOptions _options;

    public LunchCanteenBatchJob(
        ICanteenBatchService canteenBatchService,
        IBackgroundJobRunRecorder runs,
        IOptions<JobSchedulingOptions> options)
    {
        _canteenBatchService = canteenBatchService;
        _runs = runs;
        _options = options.Value;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var runId = await _runs.StartedAsync(JobNames.LunchCanteenBatch, context.NextFireTimeUtc?.UtcDateTime, context.RefireCount + 1, context.CancellationToken);
        try
        {
            await _canteenBatchService.RunBatchAsync(MealSlot.Lunch, context.CancellationToken);
            await _runs.CompletedAsync(runId, 0, "Lunch batch completed.", context.CancellationToken);
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
