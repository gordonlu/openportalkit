namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed record DashboardAlert(
    string Code,
    string Message,
    DashboardArea Area,
    string CardCode,
    string CardTitle,
    DashboardAlertLevel Level,
    string SourceModule,
    DateTimeOffset ObservedAt,
    string? ActionHref = null)
{
    public bool IsActionable => !string.IsNullOrWhiteSpace(ActionHref);
}
