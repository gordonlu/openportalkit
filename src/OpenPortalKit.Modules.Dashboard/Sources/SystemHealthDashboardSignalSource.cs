using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class SystemHealthDashboardSignalSource : IDashboardSignalSource
{
    private const string DependenciesCardCode = "system.dependencies";
    private const string DependenciesCardTitle = "System dependencies";
    private readonly IReadOnlyList<IDashboardHealthProbe> _probes;
    private readonly Func<DateTimeOffset> _clock;

    public SystemHealthDashboardSignalSource(
        IEnumerable<IDashboardHealthProbe> probes,
        Func<DateTimeOffset>? clock = null)
    {
        _probes = probes?.ToArray() ?? throw new ArgumentNullException(nameof(probes));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string SourceModule => "System";

    public async Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        var observedAt = _clock();
        var results = new List<DashboardHealthProbeResult>();

        foreach (var probe in _probes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await probe.CheckAsync(cancellationToken));
        }

        var unhealthyCount = results.Count(result => result.Status == DashboardHealthProbeStatus.Unhealthy);
        var degradedCount = results.Count(result => result.Status == DashboardHealthProbeStatus.Degraded);
        var averageLatency = results.Count == 0
            ? 0
            : Math.Round((decimal)results.Average(result => result.Latency.TotalMilliseconds), 0);

        var metrics = new List<DashboardMetricSnapshot>
        {
            Metric("system.dependencyCount", "Dependencies", results.Count, "dependencies", observedAt, 10),
            Metric("system.unhealthyDependencies", "Unhealthy", unhealthyCount, "dependencies", observedAt, 20),
            Metric("system.degradedDependencies", "Degraded", degradedCount, "dependencies", observedAt, 30),
            Metric("system.averageDependencyLatencyMs", "Avg dependency latency", averageLatency, "milliseconds", observedAt, 40)
        };

        metrics.AddRange(results
            .OrderBy(result => result.Code, StringComparer.Ordinal)
            .Select((result, index) => Metric(
                "system.dependency." + result.Code,
                result.Label,
                result.Status == DashboardHealthProbeStatus.Healthy ? 1 : 0,
                "status",
                result.ObservedAt,
                100 + index)));

        var alerts = results
            .Where(result => result.Status != DashboardHealthProbeStatus.Healthy)
            .OrderByDescending(result => result.Status)
            .ThenBy(result => result.Code, StringComparer.Ordinal)
            .Select(result => new DashboardAlert(
                "system.dependency." + result.Code,
                result.Message ?? result.Label + " is not healthy.",
                DashboardArea.SystemHealth,
                DependenciesCardCode,
                DependenciesCardTitle,
                result.Status == DashboardHealthProbeStatus.Unhealthy
                    ? DashboardAlertLevel.Critical
                    : DashboardAlertLevel.Warning,
                SourceModule,
                result.ObservedAt,
                result.ActionHref))
            .ToArray();

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
            DependenciesCardCode,
            DependenciesCardTitle,
            value,
            unit,
            observedAt,
            "System",
            sortOrder);
    }
}
