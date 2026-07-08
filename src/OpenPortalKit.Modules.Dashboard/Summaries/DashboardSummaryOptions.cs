namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed class DashboardSummaryOptions
{
    public const string SectionName = "OpenPortalKit:Dashboard";

    public int SnapshotTtlSeconds { get; set; } = 60;

    public int MaxSnapshotTtlSeconds { get; set; } = 300;
}
