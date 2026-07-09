namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed record DashboardHealthProbeResult(
    string Code,
    string Label,
    DashboardHealthProbeStatus Status,
    TimeSpan Latency,
    DateTimeOffset ObservedAt,
    string? Message = null,
    string? ActionHref = null);
