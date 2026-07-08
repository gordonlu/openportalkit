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

    public SiteOperationsDashboardSignalSource(
        IAnalyticsEventStore eventStore,
        string? siteId = null,
        int lookbackDays = 7,
        int take = 10000,
        Func<DateTimeOffset>? clock = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _siteId = siteId;
        _lookbackDays = lookbackDays;
        _take = take;
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

        var pageViews = events.Count(item => IsEvent(item, "page_view"));
        var uniqueVisitors = events
            .Where(item => IsEvent(item, "page_view"))
            .Select(item => item.HashedSessionId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var botPageViews = events.Count(item => IsEvent(item, "page_view") && item.IsBot);
        var notFoundPages = events.Count(item =>
            IsEvent(item, "page_view") &&
            item.Metadata.TryGetValue("status_code", out var statusCode) &&
            string.Equals(statusCode, "404", StringComparison.Ordinal));
        var downloads = events.Count(item => IsEvent(item, "download"));
        var formSubmissions = events.Count(item => IsEvent(item, "form_submission"));

        var metrics = new[]
        {
            Metric("site.pageViews", "Page views", pageViews, "views", observedAt, 10),
            Metric("site.uniqueVisitors", "Unique visitors", uniqueVisitors, "sessions", observedAt, 20),
            Metric("site.botPageViews", "Bot page views", botPageViews, "views", observedAt, 30),
            Metric("site.notFoundPages", "404 pages", notFoundPages, "views", observedAt, 40),
            Metric("site.downloads", "Downloads", downloads, "events", observedAt, 50),
            Metric("site.formSubmissions", "Form submissions", formSubmissions, "events", observedAt, 60)
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

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private static bool IsEvent(AnalyticsEvent analyticsEvent, string eventType)
    {
        return string.Equals(analyticsEvent.EventType, eventType, StringComparison.OrdinalIgnoreCase);
    }

    private static DashboardMetricSnapshot Metric(
        string code,
        string label,
        decimal value,
        string unit,
        DateTimeOffset observedAt,
        int sortOrder)
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
            sortOrder);
    }
}
