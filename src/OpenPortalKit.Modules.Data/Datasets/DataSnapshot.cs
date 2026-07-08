namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataSnapshot(
    Guid Id,
    Guid DataSetId,
    Guid SchemaVersionId,
    Guid SourceBatchId,
    string Format,
    string Content,
    string Checksum,
    DateTimeOffset GeneratedAt);
