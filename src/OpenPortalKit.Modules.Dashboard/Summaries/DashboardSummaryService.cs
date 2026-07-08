namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed class DashboardSummaryService
{
    private readonly DashboardAggregator _aggregator;
    private readonly IDashboardSnapshotStore _snapshotStore;
    private readonly DashboardSummaryOptions _options;
    private readonly Func<DateTimeOffset> _clock;

    public DashboardSummaryService(
        DashboardAggregator aggregator,
        IDashboardSnapshotStore snapshotStore,
        DashboardSummaryOptions options,
        Func<DateTimeOffset>? clock = null)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(
        DashboardSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = request.RequestedAt ?? _clock();
        if (!request.ForceRefresh)
        {
            var latest = await _snapshotStore.GetLatestAsync(cancellationToken);
            if (latest is not null && latest.IsFresh(now))
            {
                return latest;
            }
        }

        var summary = await _aggregator.BuildSummaryAsync(now, cancellationToken);
        var ttlSeconds = Math.Clamp(
            _options.SnapshotTtlSeconds,
            1,
            Math.Max(1, _options.MaxSnapshotTtlSeconds));
        var snapshot = new DashboardSnapshot(
            Guid.NewGuid(),
            summary,
            now,
            now.AddSeconds(ttlSeconds),
            DashboardSummaryChecksum.Compute(summary));

        await _snapshotStore.SaveAsync(snapshot, cancellationToken);
        return snapshot;
    }
}
