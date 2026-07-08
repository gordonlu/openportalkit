namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed record DashboardSummary(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DashboardCard> Cards,
    IReadOnlyList<DashboardAlert> Alerts)
{
    public int ActionableAlertCount => Cards.Sum(card => card.Alerts.Count(alert => alert.IsActionable))
        + Alerts.Count(alert => alert.IsActionable);
}
