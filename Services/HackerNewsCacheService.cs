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
    private DateTimeOffset? _lastUpdatedUtc;
    public DateTimeOffset? LastUpdatedUtc
    {
        get
        {
            lock (_sync)
            {
                return _lastUpdatedUtc;
            }
        }
    }

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
            var ids = topIds.Where(id => id > 0).Distinct().Take(Math.Clamp(_options.TopCount, 3, 30)).ToList();

            using var limiter = new SemaphoreSlim(Math.Clamp(_options.MaxConcurrentRequests, 1, 8));
            var itemTasks = ids.Select(id => GetItemAsync(httpClient, id, limiter, cts.Token));
            var results = (await Task.WhenAll(itemTasks))
                .Where(item => item is not null)
                .Cast<TechNewsFeedItem>()
                .ToList();

            if (results.Count == 0)
            {
                throw new InvalidOperationException("The Hacker News feed did not return any usable stories.");
            }

            lock (_sync)
            {
                _items = results;
                _lastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Hacker News cache. Serving previous cached payload.");
            throw;
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

            if (root.ValueKind != JsonValueKind.Object ||
                (root.TryGetProperty("deleted", out var deletedElement) && deletedElement.ValueKind == JsonValueKind.True) ||
                (root.TryGetProperty("dead", out var deadElement) && deadElement.ValueKind == JsonValueKind.True) ||
                !root.TryGetProperty("title", out var titleElement) ||
                titleElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(titleElement.GetString()))
            {
                return null;
            }

            return new TechNewsFeedItem
            {
                Id = id,
                Title = titleElement.GetString()!,
                Url = root.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String
                    ? ExternalApiUriPolicy.NormalizeAbsoluteHttpsUrl(urlElement.GetString()) ?? string.Empty
                    : string.Empty,
                By = root.TryGetProperty("by", out var byElement) && byElement.ValueKind == JsonValueKind.String
                    ? byElement.GetString() ?? string.Empty : string.Empty,
                TimeUnix = root.TryGetProperty("time", out var timeElement) && timeElement.ValueKind == JsonValueKind.Number && timeElement.TryGetInt64(out var time)
                    ? time : 0
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogWarning(ex, "Skipping unavailable Hacker News story {StoryId}.", id);
            return null;
        }
        finally
        {
            limiter.Release();
        }
    }
}
