using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class ZendeskPolicySyncService : IZendeskPolicySyncService
{
    private readonly ApplicationDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ZendeskSyncOptions _options;
    private readonly ILogger<ZendeskPolicySyncService> _logger;

    public ZendeskPolicySyncService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<ZendeskSyncOptions> options,
        ILogger<ZendeskPolicySyncService> logger)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient(nameof(ZendeskPolicySyncService));
        _options = options.Value;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogInformation("Zendesk sync skipped because base URL is not configured.");
            return;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_options.BaseUrl.TrimEnd('/')}/api/v2/help_center/{_options.Locale}/articles.json");

            if (_options.UseApiToken && !string.IsNullOrWhiteSpace(_options.Email) && !string.IsNullOrWhiteSpace(_options.ApiToken))
            {
                var raw = $"{_options.Email}/token:{_options.ApiToken}";
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 90)));

            using var response = await _httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("articles", out var articles))
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var article in articles.EnumerateArray())
            {
                var zendeskArticleId = article.GetProperty("id").GetInt64();
                var title = article.GetProperty("title").GetString() ?? "Untitled";
                var body = article.TryGetProperty("body", out var bodyNode) ? bodyNode.GetString() ?? string.Empty : string.Empty;
                var htmlUrl = article.TryGetProperty("html_url", out var urlNode) ? urlNode.GetString() ?? string.Empty : string.Empty;
                var updatedAt = article.TryGetProperty("updated_at", out var updatedNode) &&
                                DateTime.TryParse(updatedNode.GetString(), out var parsedUpdated)
                    ? parsedUpdated.ToUniversalTime()
                    : now;
                var draft = article.TryGetProperty("draft", out var draftNode) && draftNode.ValueKind == JsonValueKind.True;

                var record = await _db.ZendeskPolicyArticles
                    .FirstOrDefaultAsync(x => x.ZendeskArticleId == zendeskArticleId, cancellationToken);

                if (record is null)
                {
                    record = new ZendeskPolicyArticle
                    {
                        ZendeskArticleId = zendeskArticleId
                    };
                    _db.ZendeskPolicyArticles.Add(record);
                }

                record.Title = title;
                record.Body = body;
                record.HtmlUrl = htmlUrl;
                record.IsPublished = !draft;
                record.UpdatedAtUtc = updatedAt;
                record.VersionLabel = $"v{updatedAt:yyyyMMddHHmm}";
                record.SyncedAtUtc = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Zendesk policy sync failed; previous cache remains available.");
        }
    }
}
