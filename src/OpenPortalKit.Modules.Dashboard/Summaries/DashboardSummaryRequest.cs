namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed record DashboardSummaryRequest(
    bool ForceRefresh = false,
    DateTimeOffset? RequestedAt = null);
