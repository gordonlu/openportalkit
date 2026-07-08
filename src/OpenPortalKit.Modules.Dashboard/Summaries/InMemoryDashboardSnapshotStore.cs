namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed class InMemoryDashboardSnapshotStore : IDashboardSnapshotStore
{
    private readonly object _gate = new();
    private DashboardSnapshot? _latest;

    public Task<DashboardSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_latest);
        }
    }

    public Task SaveAsync(DashboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _latest = snapshot;
        }

        return Task.CompletedTask;
    }
}
