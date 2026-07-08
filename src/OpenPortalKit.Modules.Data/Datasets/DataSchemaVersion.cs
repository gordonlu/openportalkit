namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataSchemaVersion(
    Guid Id,
    Guid DataSetId,
    int VersionNumber,
    string SchemaJson,
    string Checksum,
    DateTimeOffset CreatedAt);
