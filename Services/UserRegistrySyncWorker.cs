using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class UserRegistrySyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserRegistrySyncWorker> _logger;
    private readonly IOptionsMonitor<UserRegistrySyncOptions> _options;

    public UserRegistrySyncWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<UserRegistrySyncWorker> logger,
        IOptionsMonitor<UserRegistrySyncOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation("[UserRegistrySync] Sync worker is disabled by configuration.");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(options.InitialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(_options.CurrentValue.SyncIntervalHours), stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IUserRegistrySyncService>();
            await syncService.SyncAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[UserRegistrySync] Sync worker is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserRegistrySync] Sync failed with exception.");
        }
    }

}
