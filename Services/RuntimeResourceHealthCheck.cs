using DigifyCXIntranet.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public sealed class RuntimeResourceHealthCheck : IHealthCheck
{
    private readonly MonitoringOptions _options;

    public RuntimeResourceHealthCheck(IOptions<MonitoringOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gc = GC.GetGCMemoryInfo();
        var snapshot = new RuntimeResourceSnapshot(
            gc.MemoryLoadBytes,
            gc.HighMemoryLoadThresholdBytes,
            gc.HeapSizeBytes,
            gc.FragmentedBytes,
            gc.TotalCommittedBytes,
            gc.PinnedObjectsCount,
            ThreadPool.ThreadCount,
            ThreadPool.PendingWorkItemCount);

        return Task.FromResult(Evaluate(snapshot, _options));
    }

    internal static HealthCheckResult Evaluate(
        RuntimeResourceSnapshot snapshot,
        MonitoringOptions options)
    {
        var warnings = new List<string>(2);
        var memoryLoadPercent = snapshot.HighMemoryLoadThresholdBytes > 0
            ? snapshot.MemoryLoadBytes * 100d / snapshot.HighMemoryLoadThresholdBytes
            : 0d;

        if (memoryLoadPercent >= options.RuntimeMemoryLoadWarningPercent)
        {
            warnings.Add($"GC memory load is {memoryLoadPercent:F1}% of the runtime high-memory threshold");
        }

        if (snapshot.PendingThreadPoolWorkItems >= options.RuntimeThreadPoolQueueWarningLength)
        {
            warnings.Add($"thread-pool queue contains {snapshot.PendingThreadPoolWorkItems} work items");
        }

        var data = new Dictionary<string, object>
        {
            ["MemoryLoadBytes"] = snapshot.MemoryLoadBytes,
            ["HighMemoryLoadThresholdBytes"] = snapshot.HighMemoryLoadThresholdBytes,
            ["HeapSizeBytes"] = snapshot.HeapSizeBytes,
            ["FragmentedBytes"] = snapshot.FragmentedBytes,
            ["TotalCommittedBytes"] = snapshot.TotalCommittedBytes,
            ["PinnedObjectsCount"] = snapshot.PinnedObjectsCount,
            ["ThreadPoolThreadCount"] = snapshot.ThreadPoolThreadCount,
            ["PendingThreadPoolWorkItems"] = snapshot.PendingThreadPoolWorkItems
        };

        return warnings.Count == 0
            ? HealthCheckResult.Healthy("Runtime resources are within configured thresholds.", data)
            : HealthCheckResult.Degraded(string.Join("; ", warnings), data: data);
    }
}

internal sealed record RuntimeResourceSnapshot(
    long MemoryLoadBytes,
    long HighMemoryLoadThresholdBytes,
    long HeapSizeBytes,
    long FragmentedBytes,
    long TotalCommittedBytes,
    long PinnedObjectsCount,
    int ThreadPoolThreadCount,
    long PendingThreadPoolWorkItems);
