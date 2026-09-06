using DigifyCXIntranet.BackgroundJobs;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public sealed class CriticalJobHealthCheck : IHealthCheck
{
    private static readonly DateTime ProcessStartedUtc = DateTime.UtcNow;
    private readonly ApplicationDbContext _db;
    private readonly MonitoringOptions _monitoring;
    private readonly JobSchedulingOptions _scheduling;
    private readonly ZendeskSyncOptions _zendesk;
    private readonly UserRegistrySyncOptions _userRegistry;
    private readonly SmtpOptions _smtp;

    public CriticalJobHealthCheck(
        ApplicationDbContext db,
        IOptions<MonitoringOptions> monitoring,
        IOptions<JobSchedulingOptions> scheduling,
        IOptions<ZendeskSyncOptions> zendesk,
        IOptions<UserRegistrySyncOptions> userRegistry,
        IOptions<SmtpOptions> smtp)
    {
        _db = db;
        _monitoring = monitoring.Value;
        _scheduling = scheduling.Value;
        _zendesk = zendesk.Value;
        _userRegistry = userRegistry.Value;
        _smtp = smtp.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var thresholds = BuildThresholds();
        var jobNames = thresholds.Keys.ToArray();
        var latest = await _db.BackgroundJobRuns
            .AsNoTracking()
            .Where(run => jobNames.Contains(run.JobName))
            .GroupBy(run => run.JobName)
            .Select(group => new
            {
                JobName = group.Key,
                LastSuccessUtc = group
                    .Where(run => run.Status == "Succeeded")
                    .Max(run => run.CompletedUtc),
                LastFailureUtc = group
                    .Where(run => run.Status == "Failed")
                    .Max(run => run.CompletedUtc)
            })
            .ToListAsync(cancellationToken);

        var data = new Dictionary<string, object>();
        var unhealthy = new List<string>();
        var now = DateTime.UtcNow;
        foreach (var (jobName, maxAge) in thresholds)
        {
            var status = latest.FirstOrDefault(item => item.JobName == jobName);
            if (status is null)
            {
                var pendingAge = now - ProcessStartedUtc;
                data[jobName] = $"PendingInitialRun:{pendingAge:c}";
                if (pendingAge > maxAge)
                {
                    unhealthy.Add(jobName);
                }
                continue;
            }

            data[$"{jobName}:LastSuccessUtc"] = status.LastSuccessUtc ?? DateTime.MinValue;
            data[$"{jobName}:LastFailureUtc"] = status.LastFailureUtc ?? DateTime.MinValue;
            if (!status.LastSuccessUtc.HasValue ||
                status.LastFailureUtc > status.LastSuccessUtc ||
                now - status.LastSuccessUtc.Value > maxAge)
            {
                unhealthy.Add(jobName);
            }
        }

        return unhealthy.Count == 0
            ? HealthCheckResult.Healthy("Enabled critical jobs are healthy or awaiting their first scheduled run.", data)
            : HealthCheckResult.Degraded(
                $"Critical jobs have failed or are stale: {string.Join(", ", unhealthy)}",
                data: data);
    }

    private Dictionary<string, TimeSpan> BuildThresholds()
    {
        var defaultThreshold = TimeSpan.FromHours(Math.Clamp(_monitoring.CriticalJobMaxAgeHours, 1, 168));
        var thresholds = new Dictionary<string, TimeSpan>
        {
            [JobNames.MonthlyPayroll] = TimeSpan.FromDays(35)
        };

        if (!string.IsNullOrWhiteSpace(_zendesk.BaseUrl))
        {
            thresholds[JobNames.ZendeskPolicySync] = Max(
                defaultThreshold,
                TimeSpan.FromHours(_scheduling.ZendeskPolicySyncIntervalHours * 2));
        }

        if (_userRegistry.Enabled)
        {
            thresholds[JobNames.UserRegistrySync] = Max(
                defaultThreshold,
                TimeSpan.FromHours(_scheduling.UserRegistrySyncIntervalHours * 2));
        }

        if (_smtp.Enabled)
        {
            thresholds[JobNames.EmailOutboxDispatch] = Max(
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(_scheduling.EmailDispatchIntervalMinutes * 5));
        }

        return thresholds;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}
