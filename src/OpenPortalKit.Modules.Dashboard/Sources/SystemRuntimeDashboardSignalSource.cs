using System.Globalization;
using OpenPortalKit.Modules.Dashboard.Analytics;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class SystemRuntimeDashboardSignalSource : IDashboardSignalSource
{
    private const string CardCode = "system.runtime";
    private const string CardTitle = "Runtime health";
    private readonly IAnalyticsEventStore _eventStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string? _siteId;
    private readonly int _lookbackDays;
    private readonly int _take;
    private readonly decimal _apiErrorRateWarningPercent;
    private readonly decimal _apiLatencyWarningMilliseconds;

    public SystemRuntimeDashboardSignalSource(
        IAnalyticsEventStore eventStore,
        string? siteId = null,
        int lookbackDays = 7,
        int take = 10000,
        decimal apiErrorRateWarningPercent = 5,
        decimal apiLatencyWarningMilliseconds = 1000,
        Func<DateTimeOffset>? clock = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _siteId = siteId;
        _lookbackDays = lookbackDays;
        _take = take;
        _apiErrorRateWarningPercent = apiErrorRateWarningPercent;
        _apiLatencyWarningMilliseconds = apiLatencyWarningMilliseconds;
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

        var apiRequests = events.Where(IsApiRequest).ToArray();
        var apiErrors = apiRequests.Count(IsErrorStatus);
        var apiAverageLatency = Average(apiRequests, "latency_ms");
        var apiErrorRate = apiRequests.Length == 0
            ? 0
            : Math.Round(apiErrors * 100m / apiRequests.Length, 1);

        var jobEvents = events.Where(IsBackgroundJob).ToArray();
        var failedJobCount = jobEvents.Count(IsFailedStatus);
        var jobSuccessRate = jobEvents.Length == 0
            ? 100
            : Math.Round(jobEvents.Count(IsSuccessStatus) * 100m / jobEvents.Length, 1);

        var searchIndexingLag = Max(events.Where(IsSearchIndexing), "lag_seconds");
        var snapshotGenerationFailures = events.Count(item =>
            IsEvent(item, "snapshot_generation") && IsFailedStatus(item));
        var revalidationFailures = events.Count(item =>
            IsEvent(item, "public_output_revalidation") && IsFailedStatus(item));

        var metrics = new[]
        {
            Metric("system.apiAverageLatencyMs", "API latency", apiAverageLatency, "milliseconds", observedAt, 10),
            Metric("system.apiErrorRate", "API error rate", apiErrorRate, "percent", observedAt, 20),
            Metric("system.backgroundJobSuccessRate", "Job success rate", jobSuccessRate, "percent", observedAt, 30),
            Metric("system.failedJobCount", "Failed jobs", failedJobCount, "jobs", observedAt, 40),
            Metric("system.searchIndexingLagSeconds", "Search indexing lag", searchIndexingLag, "seconds", observedAt, 50),
            Metric("system.snapshotGenerationFailures", "Snapshot failures", snapshotGenerationFailures, "failures", observedAt, 60),
            Metric("system.revalidationFailures", "Revalidation failures", revalidationFailures, "failures", observedAt, 70)
        };

        var alerts = new List<DashboardAlert>();
        if (apiAverageLatency >= _apiLatencyWarningMilliseconds)
        {
            alerts.Add(Alert(
                "system.apiLatency",
                "API latency is above the dashboard threshold.",
                DashboardAlertLevel.Warning,
                observedAt,
                "/admin/analytics/events"));
        }

        if (apiErrorRate >= _apiErrorRateWarningPercent)
        {
            alerts.Add(Alert(
                "system.apiErrorRate",
                "API error rate is above the dashboard threshold.",
                DashboardAlertLevel.Critical,
                observedAt,
                "/admin/analytics/events"));
        }

        if (failedJobCount > 0)
        {
            alerts.Add(Alert(
                "system.failedJobs",
                "Background jobs have failed.",
                DashboardAlertLevel.Critical,
                observedAt,
                "/admin/analytics/events"));
        }

        if (snapshotGenerationFailures > 0 || revalidationFailures > 0)
        {
            alerts.Add(Alert(
                "system.publicOutputFailures",
                "Public output generation or revalidation has failures.",
                DashboardAlertLevel.Critical,
                observedAt,
                "/admin/analytics/events"));
        }

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private static bool IsApiRequest(AnalyticsEvent analyticsEvent)
    {
        return IsEvent(analyticsEvent, "api_request");
    }

    private static bool IsBackgroundJob(AnalyticsEvent analyticsEvent)
    {
        return IsEvent(analyticsEvent, "background_job");
    }

    private static bool IsSearchIndexing(AnalyticsEvent analyticsEvent)
    {
        return IsEvent(analyticsEvent, "search_indexing");
    }

    private static bool IsEvent(AnalyticsEvent analyticsEvent, string eventType)
    {
        return string.Equals(analyticsEvent.EventType, eventType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsErrorStatus(AnalyticsEvent analyticsEvent)
    {
        return analyticsEvent.Metadata.TryGetValue("status_code", out var statusCode) &&
            int.TryParse(statusCode, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= 500;
    }

    private static bool IsFailedStatus(AnalyticsEvent analyticsEvent)
    {
        return analyticsEvent.Metadata.TryGetValue("status", out var status) &&
            string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessStatus(AnalyticsEvent analyticsEvent)
    {
        return analyticsEvent.Metadata.TryGetValue("status", out var status) &&
            (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "success", StringComparison.OrdinalIgnoreCase));
    }

    private static decimal Average(IEnumerable<AnalyticsEvent> events, string metadataKey)
    {
        var values = Values(events, metadataKey).ToArray();
        return values.Length == 0 ? 0 : Math.Round(values.Average(), 0);
    }

    private static decimal Max(IEnumerable<AnalyticsEvent> events, string metadataKey)
    {
        var values = Values(events, metadataKey).ToArray();
        return values.Length == 0 ? 0 : Math.Round(values.Max(), 0);
    }

    private static IEnumerable<decimal> Values(IEnumerable<AnalyticsEvent> events, string metadataKey)
    {
        foreach (var analyticsEvent in events)
        {
            if (analyticsEvent.Metadata.TryGetValue(metadataKey, out var value) &&
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                yield return parsed;
            }
        }
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
            DashboardArea.SystemHealth,
            CardCode,
            CardTitle,
            value,
            unit,
            observedAt,
            "Analytics",
            sortOrder);
    }

    private static DashboardAlert Alert(
        string code,
        string message,
        DashboardAlertLevel level,
        DateTimeOffset observedAt,
        string actionHref)
    {
        return new DashboardAlert(
            code,
            message,
            DashboardArea.SystemHealth,
            CardCode,
            CardTitle,
            level,
            "Analytics",
            observedAt,
            actionHref);
    }
}
