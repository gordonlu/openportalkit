using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class OutboxDashboardSignalSource : IDashboardSignalSource
{
    private const string CardCode = "system.background";
    private const string CardTitle = "System health";
    private readonly IOutboxMessageStore _outboxStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly int _batchSize;
    private readonly int _maxAttemptCount;
    private readonly int _warningThreshold;

    public OutboxDashboardSignalSource(
        IOutboxMessageStore outboxStore,
        int batchSize = 1000,
        int maxAttemptCount = 5,
        int warningThreshold = 100,
        Func<DateTimeOffset>? clock = null)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _batchSize = batchSize;
        _maxAttemptCount = maxAttemptCount;
        _warningThreshold = warningThreshold;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string SourceModule => "Jobs";

    public async Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        var observedAt = _clock();
        var pending = await _outboxStore.GetPendingAsync(_batchSize, _maxAttemptCount, cancellationToken);
        var oldestPending = pending.Count == 0
            ? 0
            : Math.Max(0, (decimal)(observedAt - pending.Min(message => message.OccurredAt)).TotalMinutes);

        var metrics = new[]
        {
            Metric("system.outboxBacklog", "Outbox backlog", pending.Count, "messages", observedAt, 10),
            Metric("system.outboxOldestPendingMinutes", "Oldest pending", Math.Round(oldestPending, 0), "minutes", observedAt, 20)
        };

        var alerts = new List<DashboardAlert>();
        if (pending.Count >= _warningThreshold)
        {
            alerts.Add(new DashboardAlert(
                "system.outboxBacklog",
                "Outbox backlog is above the dashboard threshold.",
                DashboardArea.SystemHealth,
                CardCode,
                CardTitle,
                DashboardAlertLevel.Warning,
                SourceModule,
                observedAt,
                null));
        }

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private static DashboardMetricSnapshot Metric(
        string code,
        string label,
        decimal value,
        string unit,
        DateTimeOffset observedAt,
        int sortOrder)
    {
        return new DashboardMetricSnapshot(
            code,
            label,
            DashboardArea.SystemHealth,
            CardCode,
            CardTitle,
            value,
            unit,
            observedAt,
            "Jobs",
            sortOrder);
    }
}
