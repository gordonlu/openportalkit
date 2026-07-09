using OpenPortalKit.Modules.Dashboard.Analytics;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class AgentReadinessDashboardSignalSource : IDashboardSignalSource
{
    private const string ReadinessCardCode = "agent.readiness";
    private const string ReadinessCardTitle = "Agent readiness";
    private const string TrafficCardCode = "agent.traffic";
    private const string TrafficCardTitle = "Agent traffic";
    private readonly IAgentReadinessSignalProvider _signalProvider;
    private readonly IAnalyticsEventStore _eventStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string? _siteId;
    private readonly int _lookbackDays;
    private readonly int _take;
    private readonly decimal _lowScoreThreshold;

    public AgentReadinessDashboardSignalSource(
        IAgentReadinessSignalProvider signalProvider,
        IAnalyticsEventStore eventStore,
        string? siteId = null,
        int lookbackDays = 7,
        int take = 10000,
        decimal lowScoreThreshold = 70,
        Func<DateTimeOffset>? clock = null)
    {
        _signalProvider = signalProvider ?? throw new ArgumentNullException(nameof(signalProvider));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _siteId = siteId;
        _lookbackDays = lookbackDays;
        _take = take;
        _lowScoreThreshold = lowScoreThreshold;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string SourceModule => "AgentAccess";

    public async Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        var observedAt = _clock();
        var pageSignals = await _signalProvider.ListAsync(cancellationToken);
        var events = await _eventStore.ListAsync(
            new AnalyticsEventQuery(
                SiteId: _siteId,
                From: observedAt.AddDays(-_lookbackDays),
                To: observedAt,
                Take: _take),
            cancellationToken);

        var publicOpenApiAvailable = pageSignals.Count > 0 && pageSignals.Any(signal => signal.PublicOpenApiAvailable);
        var agentEvents = events.Where(IsAgentEvent).ToArray();
        var blockedTrainingBotRequests = agentEvents.Count(IsBlockedTrainingBotRequest);
        var agentFacingErrors = pageSignals.Sum(signal => signal.AgentFacingErrorCount) +
            agentEvents.Count(IsAgentFacingError);
        var topAgentPage = TopGroup(agentEvents.Where(IsPageView), item => item.Path);

        var metrics = new[]
        {
            ReadinessMetric("agent.averageReadinessScore", "Average readiness", Average(pageSignals, signal => signal.ReadinessScore), "score", observedAt, 10),
            ReadinessMetric("agent.lowScorePages", "Low-score pages", pageSignals.Count(signal => signal.ReadinessScore < _lowScoreThreshold), "pages", observedAt, 20),
            ReadinessMetric("agent.missingMarkdownSnapshots", "Missing Markdown", pageSignals.Count(signal => !signal.HasMarkdownSnapshot), "pages", observedAt, 30),
            ReadinessMetric("agent.missingJsonSnapshots", "Missing JSON", pageSignals.Count(signal => !signal.HasJsonSnapshot), "pages", observedAt, 40),
            ReadinessMetric("agent.sitemapCoverage", "Sitemap coverage", Coverage(pageSignals, signal => signal.IncludedInSitemap), "percent", observedAt, 50),
            ReadinessMetric("agent.llmsTxtCoverage", "llms.txt coverage", Coverage(pageSignals, signal => signal.IncludedInLlmsTxt), "percent", observedAt, 60),
            ReadinessMetric("agent.structuredDataCoverage", "Structured data", Coverage(pageSignals, signal => signal.HasStructuredData), "percent", observedAt, 70),
            ReadinessMetric("agent.publicOpenApiStatus", "Public OpenAPI", publicOpenApiAvailable ? 1 : 0, "status", observedAt, 80),
            TrafficMetric("agent.aiBotTraffic", "AI bot traffic", agentEvents.Count(IsPageView), "views", observedAt, 10),
            TrafficMetric("agent.blockedTrainingBotRequests", "Blocked training bots", blockedTrainingBotRequests, "requests", observedAt, 20),
            TrafficMetric("agent.agentFacingErrors", "Agent-facing errors", agentFacingErrors, "errors", observedAt, 30),
            TrafficMetric("agent.topAgentPageViews", "Top agent page", topAgentPage.Count, "views", observedAt, 40, topAgentPage.Key)
        };

        var alerts = new List<DashboardAlert>();
        if (pageSignals.Any(signal => signal.ReadinessScore < _lowScoreThreshold))
        {
            alerts.Add(Alert(
                "agent.lowScorePages",
                "Some public pages have low agent readiness scores.",
                ReadinessCardCode,
                ReadinessCardTitle,
                DashboardAlertLevel.Warning,
                observedAt,
                "/Content"));
        }

        if (pageSignals.Any(signal => !signal.HasMarkdownSnapshot || !signal.HasJsonSnapshot))
        {
            alerts.Add(Alert(
                "agent.snapshots",
                "Some public pages are missing agent-readable snapshots.",
                ReadinessCardCode,
                ReadinessCardTitle,
                DashboardAlertLevel.Warning,
                observedAt,
                "/Content"));
        }

        if (agentFacingErrors > 0)
        {
            alerts.Add(Alert(
                "agent.errors",
                "Agents are receiving public output errors.",
                TrafficCardCode,
                TrafficCardTitle,
                DashboardAlertLevel.Critical,
                observedAt,
                "/admin/analytics/events"));
        }

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private static bool IsAgentEvent(AnalyticsEvent analyticsEvent)
    {
        if (analyticsEvent.IsBot)
        {
            return true;
        }

        return analyticsEvent.Metadata.TryGetValue("client_type", out var clientType) &&
            string.Equals(clientType, "agent", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPageView(AnalyticsEvent analyticsEvent)
    {
        return string.Equals(analyticsEvent.EventType, "page_view", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockedTrainingBotRequest(AnalyticsEvent analyticsEvent)
    {
        return analyticsEvent.Metadata.TryGetValue("bot_policy", out var policy) &&
            string.Equals(policy, "blocked_training", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAgentFacingError(AnalyticsEvent analyticsEvent)
    {
        if (!analyticsEvent.Metadata.TryGetValue("status_code", out var statusCode))
        {
            return false;
        }

        return statusCode is "404" or "500" or "502" or "503" or "504";
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

    private static decimal Average(
        IReadOnlyList<AgentReadinessPageSignal> signals,
        Func<AgentReadinessPageSignal, decimal> selector)
    {
        return signals.Count == 0 ? 0 : Math.Round(signals.Average(selector), 0);
    }

    private static decimal Coverage(
        IReadOnlyList<AgentReadinessPageSignal> signals,
        Func<AgentReadinessPageSignal, bool> selector)
    {
        return signals.Count == 0
            ? 0
            : Math.Round(signals.Count(selector) * 100m / signals.Count, 0);
    }

    private static DashboardMetricSnapshot ReadinessMetric(
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
            DashboardArea.AgentReadiness,
            ReadinessCardCode,
            ReadinessCardTitle,
            value,
            unit,
            observedAt,
            "AgentAccess",
            sortOrder,
            description);
    }

    private static DashboardMetricSnapshot TrafficMetric(
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
            DashboardArea.AgentReadiness,
            TrafficCardCode,
            TrafficCardTitle,
            value,
            unit,
            observedAt,
            "Analytics",
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
        string? actionHref)
    {
        return new DashboardAlert(
            code,
            message,
            DashboardArea.AgentReadiness,
            cardCode,
            cardTitle,
            level,
            "AgentAccess",
            observedAt,
            actionHref);
    }
}
