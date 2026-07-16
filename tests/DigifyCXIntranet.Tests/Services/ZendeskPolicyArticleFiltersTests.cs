using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using FluentAssertions;

namespace DigifyCXIntranet.Tests.Services;

public sealed class ZendeskPolicyArticleFiltersTests
{
    [Fact]
    public void InAllowedZendeskSections_ReturnsAllArticles_WhenNoAllowedSectionsAreConfigured()
    {
        var options = new ZendeskSyncOptions();
        var articles = new[]
        {
            new ZendeskPolicyArticle { ZendeskArticleId = 1, SectionId = 10 },
            new ZendeskPolicyArticle { ZendeskArticleId = 2, SectionId = 20 },
            new ZendeskPolicyArticle { ZendeskArticleId = 3, SectionId = null }
        };

        var result = articles.AsQueryable()
            .InAllowedZendeskSections(options)
            .Select(article => article.ZendeskArticleId)
            .ToList();

        result.Should().BeEquivalentTo([1L, 2L, 3L]);
    }

    [Fact]
    public void InAllowedZendeskSections_ReturnsOnlyConfiguredZendeskSections()
    {
        var options = new ZendeskSyncOptions
        {
            AllowedSectionIds = [23700658599580]
        };
        var articles = new[]
        {
            new ZendeskPolicyArticle { ZendeskArticleId = 1, SectionId = 23700658599580 },
            new ZendeskPolicyArticle { ZendeskArticleId = 2, SectionId = 123 },
            new ZendeskPolicyArticle { ZendeskArticleId = 3, SectionId = null }
        };

        var result = articles.AsQueryable()
            .InAllowedZendeskSections(options)
            .Select(article => article.ZendeskArticleId)
            .ToList();

        result.Should().Equal(1L);
    }

    [Fact]
    public void IsAllowedSection_ReportsNonAllowedSections_WhenAllowlistIsConfigured()
    {
        var allowed = ZendeskPolicyArticleFilters.GetAllowedSectionIds(new ZendeskSyncOptions
        {
            AllowedSectionIds = [23700658599580]
        });

        ZendeskPolicyArticleFilters.IsAllowedSection(23700658599580, allowed).Should().BeTrue();
        ZendeskPolicyArticleFilters.IsAllowedSection(123, allowed).Should().BeFalse();
        ZendeskPolicyArticleFilters.IsAllowedSection(null, allowed).Should().BeFalse();
    }
}
