namespace OpenPortalKit.Modules.Dashboard.Analytics;

public interface IAnalyticsEventStore
{
    Task AddAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnalyticsEvent>> ListAsync(
        AnalyticsEventQuery query,
        CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);
}
