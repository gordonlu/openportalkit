namespace OpenPortalKit.Modules.Dashboard.Summaries;

public sealed record DashboardSnapshot(
    Guid Id,
    DashboardSummary Summary,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string SourceChecksum)
{
    public bool IsFresh(DateTimeOffset asOf)
    {
        return ExpiresAt > asOf;
    }
}
