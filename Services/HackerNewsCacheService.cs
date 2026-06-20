using System.Text.Json;
using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class HackerNewsCacheService : ITechNewsCacheService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TechNewsOptions _options;
    private readonly ILogger<HackerNewsCacheService> _logger;
    private readonly object _sync = new();

    private List<TechNewsFeedItem> _items = new();
    public DateTimeOffset? LastUpdatedUtc { get; private set; }

    public HackerNewsCacheService(
        IHttpClientFactory httpClientFactory,
        IOptions<TechNewsOptions> options,
        ILogger<HackerNewsCacheService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyCollection<TechNewsFeedItem> GetCurrentItems()
    {
        lock (_sync)
        {
            return _items.ToList();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient(nameof(HackerNewsCacheService));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60)));

            var topUrl = $"{_options.BaseUrl.TrimEnd('/')}/topstories.json";
            var topIds = await httpClient.GetFromJsonAsync<List<int>>(topUrl, cts.Token) ?? new List<int>();
            var ids = topIds.Take(Math.Clamp(_options.TopCount, 3, 30)).ToList();

            using var limiter = new SemaphoreSlim(Math.Clamp(_options.MaxConcurrentRequests, 1, 8));
            var itemTasks = ids.Select(id => GetItemAsync(httpClient, id, limiter, cts.Token));
            var results = (await Task.WhenAll(itemTasks))
                .Where(item => item is not null)
                .Cast<TechNewsFeedItem>()
                .ToList();

            lock (_sync)
            {
                _items = results;
                LastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Hacker News cache. Serving previous cached payload.");
        }
    }

    private async Task<TechNewsFeedItem?> GetItemAsync(
        HttpClient httpClient,
        int id,
        SemaphoreSlim limiter,
        CancellationToken cancellationToken)
    {
        await limiter.WaitAsync(cancellationToken);
        try
        {
            var itemUrl = $"{_options.BaseUrl.TrimEnd('/')}/item/{id}.json";
            var json = await httpClient.GetStringAsync(itemUrl, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("title", out var titleElement))
            {
                return null;
            }

            return new TechNewsFeedItem
            {
                Id = id,
                Title = titleElement.GetString() ?? "Untitled",
                Url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty,
                By = root.TryGetProperty("by", out var byElement) ? byElement.GetString() ?? string.Empty : string.Empty,
                TimeUnix = root.TryGetProperty("time", out var timeElement) ? timeElement.GetInt64() : 0
            };
        }
        finally
        {
            limiter.Release();
        }
    }
}
