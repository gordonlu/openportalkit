namespace OpenPortalKit.Modules.Dashboard.Analytics;

public sealed record AnalyticsEventQuery(
    string? SiteId = null,
    string? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Skip = 0,
    int Take = 1000);
