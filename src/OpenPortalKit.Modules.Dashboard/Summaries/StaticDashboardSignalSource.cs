namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed class StaticDashboardSignalSource : IDashboardSignalSource
{
    private readonly DashboardSignalSet _signals;

    public StaticDashboardSignalSource(
        string sourceModule,
        IEnumerable<DashboardMetricSnapshot> metrics,
        IEnumerable<DashboardAlert>? alerts = null)
    {
        SourceModule = sourceModule;
        _signals = new DashboardSignalSet(
            sourceModule,
            metrics.ToArray(),
            alerts?.ToArray() ?? Array.Empty<DashboardAlert>());
    }

    public string SourceModule { get; }

    public Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_signals);
    }
}
