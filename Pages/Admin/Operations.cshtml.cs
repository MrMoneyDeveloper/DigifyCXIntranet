using DigifyCXIntranet.BackgroundJobs;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DigifyCXIntranet.Pages.Admin;

[Authorize(Policy = AppPolicies.SystemOperations)]
public class OperationsModel : PageModel
{
    private static readonly string[] KnownJobs =
    {
        JobNames.ZendeskPolicySync,
        JobNames.EmailOutboxDispatch,
        JobNames.MonthlyPayroll,
        JobNames.BreakfastCanteenBatch,
        JobNames.LunchCanteenBatch,
        JobNames.TechNewsRefresh,
        JobNames.UserRegistrySync
    };

    private readonly ApplicationDbContext _db;
    private readonly ISchedulerFactory _schedulerFactory;

    public OperationsModel(ApplicationDbContext db, ISchedulerFactory schedulerFactory)
    {
        _db = db;
        _schedulerFactory = schedulerFactory;
    }

    public List<JobStatusRow> Jobs { get; private set; } = new();
    public int PendingEmails { get; private set; }
    public int FailedEmails { get; private set; }

    public async Task OnGetAsync()
    {
        PendingEmails = await _db.EmailOutboxMessages
            .AsNoTracking()
            .CountAsync(message => message.Status == EmailOutboxStatus.Pending || message.Status == EmailOutboxStatus.Processing);
        FailedEmails = await _db.EmailOutboxMessages
            .AsNoTracking()
            .CountAsync(message => message.Status == EmailOutboxStatus.Failed);

        var scheduler = await _schedulerFactory.GetScheduler(HttpContext.RequestAborted);
        foreach (var jobName in KnownJobs)
        {
            var history = await _db.BackgroundJobRuns
                .AsNoTracking()
                .Where(run => run.JobName == jobName)
                .OrderByDescending(run => run.StartedUtc)
                .Take(100)
                .ToListAsync(HttpContext.RequestAborted);
            var latest = history.FirstOrDefault();
            var lastSuccess = history.FirstOrDefault(run => run.Status == "Succeeded")?.CompletedUtc;
            var lastFailure = history.FirstOrDefault(run => run.Status == "Failed")?.CompletedUtc;

            var jobKey = new JobKey(jobName);
            var scheduled = await scheduler.CheckExists(jobKey, HttpContext.RequestAborted);
            DateTime? nextFireUtc = null;
            if (scheduled)
            {
                var triggers = await scheduler.GetTriggersOfJob(jobKey, HttpContext.RequestAborted);
                nextFireUtc = triggers
                    .Select(trigger => trigger.GetNextFireTimeUtc()?.UtcDateTime)
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .OrderBy(value => value)
                    .FirstOrDefault();
                if (nextFireUtc == default)
                {
                    nextFireUtc = null;
                }
            }

            Jobs.Add(new JobStatusRow(
                jobName,
                scheduled,
                latest?.Status ?? "Never run",
                latest?.StartedUtc,
                lastSuccess,
                lastFailure,
                nextFireUtc,
                Math.Max(0, (latest?.Attempt ?? 1) - 1),
                latest?.ItemsProcessed ?? 0,
                Trim(latest?.Message, 300)));
        }
    }

    private static string Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Length <= maxLength ? value : value[..maxLength];

    public sealed record JobStatusRow(
        string JobName,
        bool Scheduled,
        string LatestStatus,
        DateTime? LastRunUtc,
        DateTime? LastSuccessUtc,
        DateTime? LastFailureUtc,
        DateTime? NextFireUtc,
        int RetryCount,
        int ItemsProcessed,
        string Message);
}
