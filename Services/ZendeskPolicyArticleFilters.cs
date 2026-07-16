using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;

namespace DigifyCXIntranet.Services;

public static class ZendeskPolicyArticleFilters
{
    public static IReadOnlySet<long>? GetAllowedSectionIds(ZendeskSyncOptions options)
    {
        var allowedSectionIds = options.AllowedSectionIds
            .Where(id => id > 0)
            .Distinct()
            .ToHashSet();

        return allowedSectionIds.Count == 0 ? null : allowedSectionIds;
    }

    public static IQueryable<ZendeskPolicyArticle> InAllowedZendeskSections(
        this IQueryable<ZendeskPolicyArticle> query,
        ZendeskSyncOptions options)
    {
        var allowedSectionIds = options.AllowedSectionIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (allowedSectionIds.Length == 0)
        {
            return query;
        }

        return query.Where(article =>
            article.SectionId.HasValue &&
            allowedSectionIds.Contains(article.SectionId.Value));
    }

    public static bool IsAllowedSection(long? sectionId, IReadOnlySet<long>? allowedSectionIds)
    {
        return allowedSectionIds is null ||
               (sectionId.HasValue && allowedSectionIds.Contains(sectionId.Value));
    }
}
