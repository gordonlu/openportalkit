using OpenPortalKit.Modules.Dashboard.Summaries;
using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.AdminHost.IndustryPacks;

public sealed class IndustryPackDashboardSignalSource : IDashboardSignalSource
{
    private readonly IndustryPackRuntimeRegistry _registry;

    public IndustryPackDashboardSignalSource(IndustryPackRuntimeRegistry registry)
    {
        _registry = registry;
    }

    public string SourceModule => "IndustryPacks";

    public Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var metrics = new List<DashboardMetricSnapshot>();
        foreach (var contribution in _registry.List(IndustryPackResourceKind.DashboardCard))
        {
            if (!contribution.Document.TryGetProperty("cards", out var cards)) continue;
            foreach (var card in cards.EnumerateArray())
            {
                var code = card.GetProperty("code").GetString()!;
                var title = card.GetProperty("title").GetString()!;
                var sourceMetric = card.GetProperty("metric").GetString()!;
                metrics.Add(new DashboardMetricSnapshot(
                    $"industry-pack.{contribution.PackName.ToLowerInvariant()}.{code}.active",
                    "Definition active",
                    ResolveArea(sourceMetric),
                    $"industry-pack-{contribution.PackName.ToLowerInvariant()}-{code}",
                    title,
                    1,
                    "active",
                    observedAt,
                    SourceModule,
                    Description: $"Registered by {contribution.PackName} Pack for metric '{sourceMetric}'."));
            }
        }

        return Task.FromResult(new DashboardSignalSet(SourceModule, metrics, Array.Empty<DashboardAlert>()));
    }

    private static DashboardArea ResolveArea(string metric) => metric.Split('.')[0] switch
    {
        "dataset" => DashboardArea.DataPublishing,
        "content" or "workflow" => DashboardArea.Content,
        "assets" => DashboardArea.SiteOperations,
        _ => DashboardArea.SystemHealth
    };
}
