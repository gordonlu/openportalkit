namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record PublicDataSetSchema(
    string DataSetCode,
    int VersionNumber,
    string SchemaJson,
    string Checksum,
    DateTimeOffset CreatedAt);
