namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed record DashboardCard(
    string Code,
    string Title,
    DashboardArea Area,
    IReadOnlyList<DashboardMetricSnapshot> Metrics,
    IReadOnlyList<DashboardAlert> Alerts);
