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
    private readonly PolicyDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ZendeskSyncOptions _options;
    private readonly ILogger<ZendeskPolicySyncService> _logger;

    public ZendeskPolicySyncService(
        PolicyDbContext db,
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
        var log = new ZendeskSyncLog
        {
            Operation = "PolicySync",
            StartedUtc = DateTime.UtcNow
        };
        _db.ZendeskSyncLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogInformation("Zendesk sync skipped because base URL is not configured.");
            log.CompletedUtc = DateTime.UtcNow;
            log.Succeeded = false;
            log.Message = "Zendesk base URL is not configured.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 90)));

            var categories = await GetPagedMapAsync("categories", $"api/v2/help_center/{_options.Locale}/categories.json?per_page=100", "id", "name", null, cts.Token);
            var sections = await GetPagedMapAsync("sections", $"api/v2/help_center/{_options.Locale}/sections.json?per_page=100", "id", "name", "category_id", cts.Token);
            var articles = await GetPagedElementsAsync($"api/v2/help_center/{_options.Locale}/articles.json?per_page=100", "articles", cts.Token);

            var now = DateTime.UtcNow;
            foreach (var article in articles)
            {
                var zendeskArticleId = article.GetProperty("id").GetInt64();
                var title = article.GetProperty("title").GetString() ?? "Untitled";
                var body = article.TryGetProperty("body", out var bodyNode) ? bodyNode.GetString() ?? string.Empty : string.Empty;
                var htmlUrl = article.TryGetProperty("html_url", out var urlNode) ? urlNode.GetString() ?? string.Empty : string.Empty;
                var sectionId = article.TryGetProperty("section_id", out var sectionNode) ? sectionNode.GetInt64() : 0;
                sections.TryGetValue(sectionId, out var section);
                ZendeskLookup? category = null;
                if (section is not null && section.ParentId.HasValue)
                {
                    categories.TryGetValue(section.ParentId.Value, out category);
                }

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
                record.SectionId = sectionId == 0 ? null : sectionId;
                record.SectionName = section?.Name ?? string.Empty;
                record.CategoryId = category?.Id;
                record.CategoryName = category?.Name ?? string.Empty;
            }

            log.CompletedUtc = DateTime.UtcNow;
            log.Succeeded = true;
            log.ItemsProcessed = articles.Count;
            log.Message = $"Synced {articles.Count} article(s), {sections.Count} section(s), {categories.Count} category/categories.";
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            log.CompletedUtc = DateTime.UtcNow;
            log.Succeeded = false;
            log.Message = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning(ex, "Zendesk policy sync failed; previous cache remains available.");
        }
    }

    private async Task<Dictionary<long, ZendeskLookup>> GetPagedMapAsync(
        string collectionName,
        string path,
        string idProperty,
        string nameProperty,
        string? parentIdProperty,
        CancellationToken cancellationToken)
    {
        var elements = await GetPagedElementsAsync(path, collectionName, cancellationToken);
        return elements
            .Select(x =>
            {
                var id = x.GetProperty(idProperty).GetInt64();
                long? parentId = null;
                if (parentIdProperty is not null && x.TryGetProperty(parentIdProperty, out var parentNode))
                {
                    parentId = parentNode.GetInt64();
                }

                return new ZendeskLookup(id, x.GetProperty(nameProperty).GetString() ?? string.Empty, parentId);
            })
            .ToDictionary(x => x.Id);
    }

    private async Task<List<JsonElement>> GetPagedElementsAsync(string pathOrUrl, string collectionName, CancellationToken cancellationToken)
    {
        var results = new List<JsonElement>();
        var nextPage = BuildUrl(pathOrUrl);

        while (!string.IsNullOrWhiteSpace(nextPage))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextPage);
            AddAuthorization(request);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(collectionName, out var collection))
            {
                foreach (var element in collection.EnumerateArray())
                {
                    results.Add(element.Clone());
                }
            }

            nextPage = doc.RootElement.TryGetProperty("next_page", out var nextNode) &&
                       nextNode.ValueKind == JsonValueKind.String
                ? nextNode.GetString() ?? string.Empty
                : string.Empty;
        }

        return results;
    }

    private string BuildUrl(string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        return $"{_options.BaseUrl.TrimEnd('/')}/{pathOrUrl.TrimStart('/')}";
    }

    private void AddAuthorization(HttpRequestMessage request)
    {
        if (!_options.UseApiToken || string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            return;
        }

        var raw = $"{_options.Email}/token:{_options.ApiToken}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
    }

    private record ZendeskLookup(long Id, string Name, long? ParentId);
}
