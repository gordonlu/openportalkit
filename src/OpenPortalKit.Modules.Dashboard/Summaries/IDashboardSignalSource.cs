namespace OpenPortalKit.Modules.Dashboard.Summaries;

public interface IDashboardSignalSource
{
    string SourceModule { get; }

    Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default);
}
