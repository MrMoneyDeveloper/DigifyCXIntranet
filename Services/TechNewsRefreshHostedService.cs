namespace DigifyCXIntranet.Services;

public class TechNewsRefreshHostedService : BackgroundService
{
    private readonly ITechNewsCacheService _newsCacheService;
    private readonly ILogger<TechNewsRefreshHostedService> _logger;

    public TechNewsRefreshHostedService(
        ITechNewsCacheService newsCacheService,
        ILogger<TechNewsRefreshHostedService> logger)
    {
        _newsCacheService = newsCacheService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _newsCacheService.RefreshAsync(stoppingToken);
            _logger.LogInformation("Tech news cache refresh completed.");
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
