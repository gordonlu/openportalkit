using OpenPortalKit.Modules.Content.ContentItems;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class ContentAgentReadinessSignalProvider : IAgentReadinessSignalProvider
{
    private readonly IContentItemStore _contentStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Guid? _siteId;
    private readonly int _take;
    private readonly bool _includePublishedContentInLlmsTxt;
    private readonly bool _publicOpenApiAvailable;

    public ContentAgentReadinessSignalProvider(
        IContentItemStore contentStore,
        Guid? siteId = null,
        int take = 1000,
        bool includePublishedContentInLlmsTxt = true,
        bool publicOpenApiAvailable = true,
        Func<DateTimeOffset>? clock = null)
    {
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        _siteId = siteId;
        _take = take;
        _includePublishedContentInLlmsTxt = includePublishedContentInLlmsTxt;
        _publicOpenApiAvailable = publicOpenApiAvailable;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<AgentReadinessPageSignal>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var observedAt = _clock();
        var items = await _contentStore.ListAsync(
            new ContentListQuery(SiteId: _siteId, Take: _take),
            cancellationToken);

        return items
            .Where(item => IsPubliclyVisible(item, observedAt))
            .OrderBy(item => item.Slug, StringComparer.Ordinal)
            .Select(ToSignal)
            .ToArray();
    }

    private AgentReadinessPageSignal ToSignal(ContentItem item)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(item.Title);
        var hasSummary = !string.IsNullOrWhiteSpace(item.Summary);
        var hasBody = !string.IsNullOrWhiteSpace(item.Body);
        var hasSource = !string.IsNullOrWhiteSpace(item.Source);
        var hasMarkdownSnapshot = hasTitle && hasBody;
        var hasJsonSnapshot = hasTitle &&
            !string.IsNullOrWhiteSpace(item.Slug) &&
            hasSummary &&
            item.PublishedAt is not null;
        var includedInSitemap = item.PublishedAt is not null;
        var hasStructuredData = hasTitle && hasSummary && item.PublishedAt is not null;

        return new AgentReadinessPageSignal(
            item.Id.ToString("N"),
            "/content/" + item.Slug.Trim('/'),
            Score(
                hasTitle,
                hasSummary,
                hasBody,
                hasSource,
                hasMarkdownSnapshot,
                hasJsonSnapshot,
                includedInSitemap,
                _includePublishedContentInLlmsTxt,
                hasStructuredData,
                _publicOpenApiAvailable),
            hasMarkdownSnapshot,
            hasJsonSnapshot,
            includedInSitemap,
            _includePublishedContentInLlmsTxt,
            hasStructuredData,
            _publicOpenApiAvailable);
    }

    private static bool IsPubliclyVisible(ContentItem item, DateTimeOffset observedAt)
    {
        return item.Status == ContentPublicationStatus.Published &&
            item.PublishedAt is not null &&
            item.PublishedAt <= observedAt &&
            (item.ExpiresAt is null || item.ExpiresAt > observedAt);
    }

    private static decimal Score(params bool[] checks)
    {
        return checks.Length == 0
            ? 0
            : Math.Round(checks.Count(check => check) * 100m / checks.Length, 0);
    }
}
