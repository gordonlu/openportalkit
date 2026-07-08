namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataRecord(
    Guid Id,
    Guid DataSetId,
    string RecordKey,
    string PayloadJson,
    DateOnly AsOfDate,
    Guid SchemaVersionId,
    Guid SourceBatchId,
    string Source,
    string Checksum,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
