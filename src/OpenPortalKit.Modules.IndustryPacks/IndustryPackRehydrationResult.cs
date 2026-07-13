namespace OpenPortalKit.Modules.IndustryPacks;

public sealed record IndustryPackRehydrationResult(
    int RehydratedPackCount,
    int RehydratedResourceCount,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}
