namespace OpenPortalKit.Modules.IndustryPacks;

public interface IIndustryPackResourceRegistrationTarget
{
    IndustryPackResourceKind Kind { get; }

    bool RequiresStartupRehydration { get; }

    Task<IReadOnlyList<string>> ValidateAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken = default);

    Task DisableAsync(
        LoadedIndustryPack pack,
        CancellationToken cancellationToken = default);
}
