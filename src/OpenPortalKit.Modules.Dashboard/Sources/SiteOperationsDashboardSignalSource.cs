using OpenPortalKit.Modules.Dashboard.Analytics;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class SiteOperationsDashboardSignalSource : IDashboardSignalSource
{
    private const string CardCode = "site.operations";
    private const string CardTitle = "Site operations";
    private readonly IAnalyticsEventStore _eventStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string? _siteId;
    private readonly int _lookbackDays;
    private readonly int _take;
    private readonly decimal _slowPageThresholdMilliseconds;

    public SiteOperationsDashboardSignalSource(
        IAnalyticsEventStore eventStore,
        string? siteId = null,
        int lookbackDays = 7,
        int take = 10000,
        decimal slowPageThresholdMilliseconds = 2000,
        Func<DateTimeOffset>? clock = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _siteId = siteId;
        _lookbackDays = lookbackDays;
        _take = take;
        _slowPageThresholdMilliseconds = slowPageThresholdMilliseconds;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string SourceModule => "Analytics";

    public async Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        var observedAt = _clock();
        var events = await _eventStore.ListAsync(
            new AnalyticsEventQuery(
                SiteId: _siteId,
                From: observedAt.AddDays(-_lookbackDays),
                To: observedAt,
                Take: _take),
            cancellationToken);

        var pageViewEvents = events.Where(item => IsEvent(item, "page_view")).ToArray();
        var pageViews = pageViewEvents.Length;
        var uniqueVisitors = pageViewEvents
            .Select(item => item.HashedSessionId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var botPageViews = pageViewEvents.Count(item => item.IsBot);
        var notFoundPages = pageViewEvents.Count(item =>
            item.Metadata.TryGetValue("status_code", out var statusCode) &&
            string.Equals(statusCode, "404", StringComparison.Ordinal));
        var downloads = events.Count(item => IsEvent(item, "download"));
        var formSubmissions = events.Count(item => IsEvent(item, "form_submission"));
        var activityRegistrations = events.Count(item => IsEvent(item, "activity_registration"));
        var topPage = TopGroup(pageViewEvents, item => item.Path);
        var topSection = TopGroup(pageViewEvents, item => FirstPathSegment(item.Path));
        var trafficSources = pageViewEvents
            .Select(TrafficSource)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var topTrafficSource = TopGroup(pageViewEvents, TrafficSource);
        var searchKeywords = pageViewEvents
            .Select(item => MetadataValue(item, "search_keyword"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var topSearchKeyword = TopGroup(pageViewEvents, item => MetadataValue(item, "search_keyword"));
        var topEntryPage = TopGroup(
            pageViewEvents.Where(item => IsMetadataTrue(item, "is_entry")),
            item => item.Path);
        var topExitPage = TopGroup(
            pageViewEvents.Where(item => IsMetadataTrue(item, "is_exit")),
            item => item.Path);
        var slowPages = pageViewEvents.Count(IsSlowPage);

        var metrics = new[]
        {
            Metric("site.pageViews", "Page views", pageViews, "views", observedAt, 10),
            Metric("site.uniqueVisitors", "Unique visitors", uniqueVisitors, "sessions", observedAt, 20),
            Metric("site.botPageViews", "Bot page views", botPageViews, "views", observedAt, 30),
            Metric("site.notFoundPages", "404 pages", notFoundPages, "views", observedAt, 40),
            Metric("site.downloads", "Downloads", downloads, "events", observedAt, 50),
            Metric("site.formSubmissions", "Form submissions", formSubmissions, "events", observedAt, 60),
            Metric("site.activityRegistrations", "Activity registrations", activityRegistrations, "events", observedAt, 70),
            Metric("site.topPageViews", "Top page views", topPage.Count, "views", observedAt, 80, topPage.Key),
            Metric("site.topSectionViews", "Top section views", topSection.Count, "views", observedAt, 90, topSection.Key),
            Metric("site.trafficSources", "Traffic sources", trafficSources.Length, "sources", observedAt, 100, topTrafficSource.Key),
            Metric("site.searchKeywords", "Search keywords", searchKeywords.Length, "keywords", observedAt, 110, topSearchKeyword.Key),
            Metric("site.topEntryPageViews", "Top entry page", topEntryPage.Count, "views", observedAt, 120, topEntryPage.Key),
            Metric("site.topExitPageViews", "Top exit page", topExitPage.Count, "views", observedAt, 130, topExitPage.Key),
            Metric("site.slowPages", "Slow pages", slowPages, "views", observedAt, 140)
        };

        var alerts = new List<DashboardAlert>();
        if (notFoundPages > 0)
        {
            alerts.Add(new DashboardAlert(
                "site.notFoundPages",
                "Visitors are hitting missing public pages.",
                DashboardArea.SiteOperations,
                CardCode,
                CardTitle,
                DashboardAlertLevel.Warning,
                SourceModule,
                observedAt,
                null));
        }

        if (slowPages > 0)
        {
            alerts.Add(new DashboardAlert(
                "site.slowPages",
                "Some public pages are slower than the dashboard threshold.",
                DashboardArea.SiteOperations,
                CardCode,
                CardTitle,
                DashboardAlertLevel.Warning,
                SourceModule,
                observedAt,
                null));
        }

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private static bool IsEvent(AnalyticsEvent analyticsEvent, string eventType)
    {
        return string.Equals(analyticsEvent.EventType, eventType, StringComparison.OrdinalIgnoreCase);
    }

    private static (string? Key, int Count) TopGroup(
        IEnumerable<AnalyticsEvent> events,
        Func<AnalyticsEvent, string?> keySelector)
    {
        var top = events
            .Select(keySelector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Key = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return top is null ? (null, 0) : (top.Key, top.Count);
    }

    private static string? MetadataValue(AnalyticsEvent analyticsEvent, string key)
    {
        return analyticsEvent.Metadata.TryGetValue(key, out var value) ? value : null;
    }

    private static bool IsMetadataTrue(AnalyticsEvent analyticsEvent, string key)
    {
        return analyticsEvent.Metadata.TryGetValue(key, out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TrafficSource(AnalyticsEvent analyticsEvent)
    {
        var explicitSource = MetadataValue(analyticsEvent, "traffic_source");
        if (!string.IsNullOrWhiteSpace(explicitSource))
        {
            return explicitSource;
        }

        if (!string.IsNullOrWhiteSpace(analyticsEvent.Referrer) &&
            Uri.TryCreate(analyticsEvent.Referrer, UriKind.Absolute, out var referrer))
        {
            return referrer.Host;
        }

        return null;
    }

    private static string? FirstPathSegment(string path)
    {
        return path
            .Split('?', 2)[0]
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    }

    private bool IsSlowPage(AnalyticsEvent analyticsEvent)
    {
        if (!analyticsEvent.Metadata.TryGetValue("latency_ms", out var latency) ||
            !decimal.TryParse(
                latency,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
        {
            return false;
        }

        return parsed >= _slowPageThresholdMilliseconds;
    }

    private static DashboardMetricSnapshot Metric(
        string code,
        string label,
        decimal value,
        string unit,
        DateTimeOffset observedAt,
        int sortOrder,
        string? description = null)
    {
        return new DashboardMetricSnapshot(
            code,
            label,
            DashboardArea.SiteOperations,
            CardCode,
            CardTitle,
            value,
            unit,
            observedAt,
            "Analytics",
            sortOrder,
            description);
    }
}
