using OpenPortalKit.Modules.Content;
using OpenPortalKit.Modules.Content.ContentItems;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class ContentDashboardSignalSource : IDashboardSignalSource
{
    private const string PublishingCardCode = "content.publishing";
    private const string PublishingCardTitle = "Content publishing";
    private const string ReadinessCardCode = "content.readiness";
    private const string ReadinessCardTitle = "Content readiness";
    private readonly IContentItemStore _contentStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Guid? _siteId;
    private readonly int _staleAfterDays;
    private readonly int _take;

    public ContentDashboardSignalSource(
        IContentItemStore contentStore,
        Guid? siteId = null,
        int staleAfterDays = 90,
        int take = 1000,
        Func<DateTimeOffset>? clock = null)
    {
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        _siteId = siteId;
        _staleAfterDays = staleAfterDays;
        _take = take;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string SourceModule => ContentModule.Descriptor.Name;

    public async Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        var observedAt = _clock();
        var items = await _contentStore.ListAsync(
            new ContentListQuery(SiteId: _siteId, Take: _take),
            cancellationToken);

        var today = DateOnly.FromDateTime(observedAt.UtcDateTime);
        var staleBefore = observedAt.AddDays(-_staleAfterDays);
        var publicItems = items.Where(item => item.Status == ContentPublicationStatus.Published).ToArray();

        var draftCount = Count(items, ContentPublicationStatus.Draft);
        var reviewQueueCount = Count(items, ContentPublicationStatus.Review);
        var rejectedCount = Count(items, ContentPublicationStatus.Rejected);
        var publishedTodayCount = publicItems.Count(item =>
            item.PublishedAt is not null &&
            DateOnly.FromDateTime(item.PublishedAt.Value.UtcDateTime) == today);
        var scheduledCount = items.Count(item =>
            item.ScheduledAt is not null &&
            item.ScheduledAt > observedAt &&
            item.Status is ContentPublicationStatus.Draft or ContentPublicationStatus.Approved);
        var archivedCount = Count(items, ContentPublicationStatus.Archived);
        var staleCount = publicItems.Count(item => item.UpdatedAt < staleBefore);
        var missingSeoMetadataCount = publicItems.Count(item =>
            string.IsNullOrWhiteSpace(item.Title) ||
            string.IsNullOrWhiteSpace(item.Summary) ||
            string.IsNullOrWhiteSpace(item.Source));
        var missingSummaryCount = publicItems.Count(item => string.IsNullOrWhiteSpace(item.Summary));
        var missingCoverCount = publicItems.Count(item => item.CoverAssetId is null);
        var missingAgentSnapshotsCount = publicItems.Count(item =>
            string.IsNullOrWhiteSpace(item.Title) ||
            string.IsNullOrWhiteSpace(item.Slug) ||
            string.IsNullOrWhiteSpace(item.Summary) ||
            string.IsNullOrWhiteSpace(item.Body));
        var topContentType = TopGroup(publicItems, item => item.ContentTypeId.ToString("N"));
        var topAuthor = TopGroup(publicItems.Where(item => item.AuthorId is not null), item => item.AuthorId?.ToString("N"));
        var topCategory = TopGroup(publicItems.Where(item => item.CategoryId is not null), item => item.CategoryId?.ToString("N"));

        var metrics = new[]
        {
            Metric("content.draftCount", "Drafts", PublishingCardCode, PublishingCardTitle, draftCount, observedAt, 10),
            Metric("content.reviewQueue", "Review queue", PublishingCardCode, PublishingCardTitle, reviewQueueCount, observedAt, 20),
            Metric("content.rejectedCount", "Rejected", PublishingCardCode, PublishingCardTitle, rejectedCount, observedAt, 30),
            Metric("content.publishedToday", "Published today", PublishingCardCode, PublishingCardTitle, publishedTodayCount, observedAt, 40),
            Metric("content.scheduledCount", "Scheduled", PublishingCardCode, PublishingCardTitle, scheduledCount, observedAt, 50),
            Metric("content.archivedCount", "Archived", PublishingCardCode, PublishingCardTitle, archivedCount, observedAt, 60),
            Metric("content.staleCount", "Stale content", ReadinessCardCode, ReadinessCardTitle, staleCount, observedAt, 10),
            Metric("content.missingSeoMetadata", "Missing SEO metadata", ReadinessCardCode, ReadinessCardTitle, missingSeoMetadataCount, observedAt, 20),
            Metric("content.missingSummary", "Missing summary", ReadinessCardCode, ReadinessCardTitle, missingSummaryCount, observedAt, 30),
            Metric("content.missingCover", "Missing cover", ReadinessCardCode, ReadinessCardTitle, missingCoverCount, observedAt, 40),
            Metric("content.missingAgentSnapshots", "Missing AgentSEO snapshots", ReadinessCardCode, ReadinessCardTitle, missingAgentSnapshotsCount, observedAt, 50),
            Metric("content.topContentTypeCount", "Top content type", ReadinessCardCode, ReadinessCardTitle, topContentType.Count, observedAt, 60, topContentType.Key),
            Metric("content.topAuthorCount", "Top author", ReadinessCardCode, ReadinessCardTitle, topAuthor.Count, observedAt, 70, topAuthor.Key),
            Metric("content.topCategoryCount", "Top category", ReadinessCardCode, ReadinessCardTitle, topCategory.Count, observedAt, 80, topCategory.Key)
        };

        var alerts = new List<DashboardAlert>();
        if (reviewQueueCount > 0)
        {
            alerts.Add(Alert(
                "content.reviewQueue",
                "Content is waiting for review.",
                PublishingCardCode,
                PublishingCardTitle,
                DashboardAlertLevel.Info,
                observedAt,
                "/Content"));
        }

        if (staleCount > 0)
        {
            alerts.Add(Alert(
                "content.stale",
                "Published content has not been updated recently.",
                ReadinessCardCode,
                ReadinessCardTitle,
                DashboardAlertLevel.Warning,
                observedAt,
                "/Content"));
        }

        if (missingSummaryCount > 0)
        {
            alerts.Add(Alert(
                "content.summary",
                "Published content is missing summaries.",
                ReadinessCardCode,
                ReadinessCardTitle,
                DashboardAlertLevel.Warning,
                observedAt,
                "/Content"));
        }

        if (missingSeoMetadataCount > 0)
        {
            alerts.Add(Alert(
                "content.seoMetadata",
                "Published content is missing SEO metadata inputs.",
                ReadinessCardCode,
                ReadinessCardTitle,
                DashboardAlertLevel.Warning,
                observedAt,
                "/Content"));
        }

        if (missingAgentSnapshotsCount > 0)
        {
            alerts.Add(Alert(
                "content.agentSnapshots",
                "Published content is missing agent-readable snapshot inputs.",
                ReadinessCardCode,
                ReadinessCardTitle,
                DashboardAlertLevel.Warning,
                observedAt,
                "/Content"));
        }

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private static int Count(IEnumerable<ContentItem> items, ContentPublicationStatus status)
    {
        return items.Count(item => item.Status == status);
    }

    private static (string? Key, int Count) TopGroup(
        IEnumerable<ContentItem> items,
        Func<ContentItem, string?> keySelector)
    {
        var top = items
            .Select(keySelector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Key = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return top is null ? (null, 0) : (top.Key, top.Count);
    }

    private static DashboardMetricSnapshot Metric(
        string code,
        string label,
        string cardCode,
        string cardTitle,
        decimal value,
        DateTimeOffset observedAt,
        int sortOrder,
        string? description = null)
    {
        return new DashboardMetricSnapshot(
            code,
            label,
            DashboardArea.Content,
            cardCode,
            cardTitle,
            value,
            "items",
            observedAt,
            ContentModule.Descriptor.Name,
            sortOrder,
            description);
    }

    private static DashboardAlert Alert(
        string code,
        string message,
        string cardCode,
        string cardTitle,
        DashboardAlertLevel level,
        DateTimeOffset observedAt,
        string actionHref)
    {
        return new DashboardAlert(
            code,
            message,
            DashboardArea.Content,
            cardCode,
            cardTitle,
            level,
            ContentModule.Descriptor.Name,
            observedAt,
            actionHref);
    }
}
