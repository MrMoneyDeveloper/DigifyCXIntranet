using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.Extensions.Options;
using Quartz;

namespace DigifyCXIntranet.BackgroundJobs;

[DisallowConcurrentExecution]
public class EmailOutboxDispatchJob : IJob
{
    private readonly IEmailOutboxDispatcher _dispatcher;
    private readonly IBackgroundJobRunRecorder _runs;
    private readonly JobSchedulingOptions _options;

    public EmailOutboxDispatchJob(
        IEmailOutboxDispatcher dispatcher,
        IBackgroundJobRunRecorder runs,
        IOptions<JobSchedulingOptions> options)
    {
        _dispatcher = dispatcher;
        _runs = runs;
        _options = options.Value;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var runId = await _runs.StartedAsync(JobNames.EmailOutboxDispatch, context.NextFireTimeUtc?.UtcDateTime, context.RefireCount + 1, context.CancellationToken);
        try
        {
            var sent = await _dispatcher.DispatchPendingAsync(_options.EmailDispatchBatchSize, context.CancellationToken);
            await _runs.CompletedAsync(runId, sent, $"Dispatched={sent}", context.CancellationToken);
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
