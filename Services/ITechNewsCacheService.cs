namespace DigifyCXIntranet.Services;

public interface ITechNewsCacheService
{
    IReadOnlyCollection<TechNewsFeedItem> GetCurrentItems();
    DateTimeOffset? LastUpdatedUtc { get; }
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
