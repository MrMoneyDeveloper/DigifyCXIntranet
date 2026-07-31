using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using Microsoft.Extensions.Options;
using Quartz;

namespace DigifyCXIntranet.BackgroundJobs;

[DisallowConcurrentExecution]
public class MonthlyPayrollJob : IJob
{
    private readonly IMonthlyPayrollRunner _runner;
    private readonly IBackgroundJobRunRecorder _runs;
    private readonly JobSchedulingOptions _options;

    public MonthlyPayrollJob(
        IMonthlyPayrollRunner runner,
        IBackgroundJobRunRecorder runs,
        IOptions<JobSchedulingOptions> options)
    {
        _runner = runner;
        _runs = runs;
        _options = options.Value;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var runId = await _runs.StartedAsync(JobNames.MonthlyPayroll, context.NextFireTimeUtc?.UtcDateTime, context.RefireCount + 1, context.CancellationToken);
        try
        {
            var rows = await _runner.RunAsync(context.CancellationToken);
            await _runs.CompletedAsync(runId, rows, $"Payroll job completed. Employees={rows}.", context.CancellationToken);
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
