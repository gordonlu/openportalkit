namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record PublicDataRecord(
    string RecordKey,
    string PayloadJson,
    DateOnly AsOfDate,
    Guid SchemaVersionId,
    Guid SourceBatchId,
    string Source,
    string Checksum,
    DateTimeOffset UpdatedAt);
