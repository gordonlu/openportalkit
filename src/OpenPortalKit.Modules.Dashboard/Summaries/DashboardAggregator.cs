namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed class DashboardAggregator
{
    private readonly IReadOnlyList<IDashboardSignalSource> _sources;

    public DashboardAggregator(IEnumerable<IDashboardSignalSource> sources)
    {
        _sources = sources?.ToArray() ?? throw new ArgumentNullException(nameof(sources));
    }

    public async Task<DashboardSummary> BuildSummaryAsync(
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken = default)
    {
        var signalSets = new List<DashboardSignalSet>();

        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            signalSets.Add(await source.CollectAsync(cancellationToken));
        }

        var metrics = signalSets.SelectMany(set => set.Metrics);
        var alerts = signalSets.SelectMany(set => set.Alerts);

        var cards = metrics
            .GroupBy(metric => new { metric.Area, metric.CardCode, metric.CardTitle })
            .Select(group =>
            {
                var cardAlerts = alerts
                    .Where(alert =>
                        alert.Area == group.Key.Area &&
                        string.Equals(alert.CardCode, group.Key.CardCode, StringComparison.Ordinal))
                    .OrderByDescending(alert => alert.Level)
                    .ThenBy(alert => alert.Code, StringComparer.Ordinal)
                    .ToArray();

                return new DashboardCard(
                    group.Key.CardCode,
                    group.Key.CardTitle,
                    group.Key.Area,
                    group
                        .OrderBy(metric => metric.SortOrder)
                        .ThenBy(metric => metric.Code, StringComparer.Ordinal)
                        .ToArray(),
                    cardAlerts);
            })
            .OrderBy(card => card.Area)
            .ThenBy(card => card.Code, StringComparer.Ordinal)
            .ToArray();

        var orphanAlerts = alerts
            .Where(alert => !cards.Any(card =>
                card.Area == alert.Area &&
                string.Equals(card.Code, alert.CardCode, StringComparison.Ordinal)))
            .OrderBy(alert => alert.Area)
            .ThenBy(alert => alert.CardCode, StringComparer.Ordinal)
            .ThenBy(alert => alert.Code, StringComparer.Ordinal)
            .ToArray();

        return new DashboardSummary(generatedAt, cards, orphanAlerts);
    }
}
