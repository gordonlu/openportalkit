namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed record DashboardSignalSet(
    string SourceModule,
    IReadOnlyList<DashboardMetricSnapshot> Metrics,
    IReadOnlyList<DashboardAlert> Alerts)
{
    public static DashboardSignalSet Empty(string sourceModule)
    {
        return new DashboardSignalSet(
            sourceModule,
            Array.Empty<DashboardMetricSnapshot>(),
            Array.Empty<DashboardAlert>());
    }
}
