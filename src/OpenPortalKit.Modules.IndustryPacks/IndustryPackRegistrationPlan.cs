namespace OpenPortalKit.Modules.IndustryPacks;

public enum IndustryPackResourceChangeType
{
    Add,
    Update,
    Unchanged,
    Remove
}

public sealed record IndustryPackResourceChange(
    IndustryPackResourceKind Kind,
    string ResourcePath,
    IndustryPackResourceChangeType ChangeType,
    string? PreviousChecksum,
    string? CurrentChecksum);

public sealed record IndustryPackRegistrationPlan(
    string PackName,
    string Version,
    bool IsCurrentlyEnabled,
    bool ManifestChanged,
    IReadOnlyList<IndustryPackResourceChange> Changes)
{
    public int ChangedResourceCount => Changes.Count(change => change.ChangeType != IndustryPackResourceChangeType.Unchanged);
}

public sealed record IndustryPackOperationResult(
    bool Succeeded,
    IndustryPackInstallation? Installation,
    IndustryPackRegistrationPlan Plan,
    IReadOnlyList<string> Errors);
