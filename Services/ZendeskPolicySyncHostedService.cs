namespace DigifyCXIntranet.Services;

public class ZendeskPolicySyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ZendeskPolicySyncHostedService> _logger;

    public ZendeskPolicySyncHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ZendeskPolicySyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IZendeskPolicySyncService>();
            await syncService.SyncAsync(stoppingToken);
            _logger.LogInformation("Zendesk policy sync cycle completed.");
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
