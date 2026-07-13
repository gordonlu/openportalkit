namespace OpenPortalKit.Modules.IndustryPacks;

public sealed record IndustryPackInstallation(
    string PackName,
    string Version,
    string ManifestChecksum,
    bool IsEnabled,
    Guid UpdatedBy,
    DateTimeOffset InstalledAt,
    DateTimeOffset UpdatedAt);

public sealed record IndustryPackResourceRegistration(
    string PackName,
    string ResourcePath,
    IndustryPackResourceKind Kind,
    string Checksum,
    DateTimeOffset RegisteredAt);
