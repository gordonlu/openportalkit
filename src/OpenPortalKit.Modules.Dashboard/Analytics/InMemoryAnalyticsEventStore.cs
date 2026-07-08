namespace OpenPortalKit.Modules.Dashboard.Analytics;

public sealed class InMemoryAnalyticsEventStore : IAnalyticsEventStore
{
    private readonly object _gate = new();
    private readonly List<AnalyticsEvent> _events = new();

    public Task AddAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _events.RemoveAll(existing => existing.Id == analyticsEvent.Id);
            _events.Add(analyticsEvent);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AnalyticsEvent>> ListAsync(
        AnalyticsEventQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Take);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var results = _events
                .Where(item => query.SiteId is null ||
                    string.Equals(item.SiteId, query.SiteId, StringComparison.Ordinal))
                .Where(item => query.EventType is null ||
                    string.Equals(item.EventType, query.EventType, StringComparison.OrdinalIgnoreCase))
                .Where(item => query.From is null || item.OccurredAt >= query.From)
                .Where(item => query.To is null || item.OccurredAt <= query.To)
                .OrderByDescending(item => item.OccurredAt)
                .ThenBy(item => item.Id)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToArray();

            return Task.FromResult<IReadOnlyList<AnalyticsEvent>>(results);
        }
    }

    public Task<int> DeleteOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var removed = _events.RemoveAll(item => item.OccurredAt < cutoff);
            return Task.FromResult(removed);
        }
    }
}
