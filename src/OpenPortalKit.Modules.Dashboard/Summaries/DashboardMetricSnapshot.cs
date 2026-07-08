namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed record DashboardMetricSnapshot(
    string Code,
    string Label,
    DashboardArea Area,
    string CardCode,
    string CardTitle,
    decimal Value,
    string Unit,
    DateTimeOffset ObservedAt,
    string SourceModule,
    int SortOrder = 0,
    string? Description = null);
