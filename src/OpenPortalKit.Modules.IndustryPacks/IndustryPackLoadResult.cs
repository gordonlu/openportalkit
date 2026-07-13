using System.Text.Json;

namespace OpenPortalKit.Modules.IndustryPacks;

public sealed record IndustryPackValidationError(string Code, string Message, string? ResourcePath = null);

public sealed record IndustryPackResource(
    IndustryPackResourceKind Kind,
    string RelativePath,
    string Checksum,
    JsonElement Document);

public sealed record LoadedIndustryPack(
    IndustryPackManifest Manifest,
    string RootPath,
    string ManifestChecksum,
    IReadOnlyList<IndustryPackResource> Resources);

public sealed record IndustryPackLoadResult(
    LoadedIndustryPack? Pack,
    IReadOnlyList<IndustryPackValidationError> Errors)
{
    public bool Succeeded => Pack is not null && Errors.Count == 0;
}

public sealed record IndustryPackCatalogResult(
    IReadOnlyList<LoadedIndustryPack> Packs,
    IReadOnlyList<IndustryPackValidationError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}
