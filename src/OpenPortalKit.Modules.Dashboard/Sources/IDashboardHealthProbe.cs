namespace OpenPortalKit.Modules.Dashboard.Sources;

public interface IDashboardHealthProbe
{
    string Code { get; }

    Task<DashboardHealthProbeResult> CheckAsync(CancellationToken cancellationToken = default);
}
