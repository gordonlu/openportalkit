namespace OpenPortalKit.Modules.Dashboard.Summaries;

public interface IDashboardSnapshotStore
{
    Task<DashboardSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DashboardSnapshot snapshot, CancellationToken cancellationToken = default);
}
