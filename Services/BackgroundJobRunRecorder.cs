using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;

namespace DigifyCXIntranet.Services;

public class BackgroundJobRunRecorder : IBackgroundJobRunRecorder
{
    private readonly ApplicationDbContext _db;

    public BackgroundJobRunRecorder(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<long> StartedAsync(string jobName, DateTime? nextFireUtc, int attempt, CancellationToken cancellationToken)
    {
        var run = new BackgroundJobRun
        {
            JobName = jobName,
            Status = "Started",
            StartedUtc = DateTime.UtcNow,
            NextFireUtc = nextFireUtc,
            Attempt = Math.Max(1, attempt)
        };

        _db.BackgroundJobRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public async Task CompletedAsync(long runId, int itemsProcessed, string message, CancellationToken cancellationToken)
    {
        var run = await _db.BackgroundJobRuns.FindAsync(new object[] { runId }, cancellationToken);
        if (run is null)
        {
            return;
        }

        run.Status = "Succeeded";
        run.CompletedUtc = DateTime.UtcNow;
        run.ItemsProcessed = itemsProcessed;
        run.Message = Trim(message, 1000);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailedAsync(long runId, string message, CancellationToken cancellationToken)
    {
        var run = await _db.BackgroundJobRuns.FindAsync(new object[] { runId }, cancellationToken);
        if (run is null)
        {
            return;
        }

        run.Status = "Failed";
        run.CompletedUtc = DateTime.UtcNow;
        run.Message = Trim(message, 1000);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string Trim(string value, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Length <= maxLength ? value : value[..maxLength];
    }
}
