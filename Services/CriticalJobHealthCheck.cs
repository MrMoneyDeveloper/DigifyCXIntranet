using DigifyCXIntranet.BackgroundJobs;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class CriticalJobHealthCheck : IHealthCheck
{
    private static readonly string[] CriticalJobs =
    {
        JobNames.ZendeskPolicySync,
        JobNames.UserRegistrySync,
        JobNames.EmailOutboxDispatch,
        JobNames.MonthlyPayroll
    };

    private readonly ApplicationDbContext _db;
    private readonly MonitoringOptions _options;

    public CriticalJobHealthCheck(ApplicationDbContext db, IOptions<MonitoringOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow.AddHours(-Math.Clamp(_options.CriticalJobMaxAgeHours, 1, 168));
        var latest = await _db.BackgroundJobRuns
            .AsNoTracking()
            .Where(x => CriticalJobs.Contains(x.JobName) && x.Status == "Succeeded")
            .GroupBy(x => x.JobName)
            .Select(x => new { JobName = x.Key, CompletedUtc = x.Max(v => v.CompletedUtc) })
            .ToListAsync(cancellationToken);

        var data = latest.ToDictionary(x => x.JobName, x => (object)(x.CompletedUtc ?? DateTime.MinValue));
        var staleJobs = CriticalJobs
            .Where(job => !data.TryGetValue(job, out var completed) || completed is not DateTime dt || dt < threshold)
            .ToList();

        return staleJobs.Count == 0
            ? HealthCheckResult.Healthy("Critical background jobs have recent successful runs.", data)
            : HealthCheckResult.Degraded($"Critical jobs are stale or have not completed: {string.Join(", ", staleJobs)}", data: data);
    }
}
