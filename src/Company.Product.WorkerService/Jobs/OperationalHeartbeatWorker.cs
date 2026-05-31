namespace Company.Product.WorkerService.Jobs;

public sealed class OperationalHeartbeatWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly ILogger<OperationalHeartbeatWorker> _logger;

    public OperationalHeartbeatWorker(ILogger<OperationalHeartbeatWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker heartbeat at {UtcNow}", DateTime.UtcNow);
            await Task.Delay(Interval, stoppingToken);
        }
    }
}
