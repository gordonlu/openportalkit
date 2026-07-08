namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataImportBatch(
    Guid Id,
    Guid DataSetId,
    Guid SchemaVersionId,
    string Source,
    string? SourceFileName,
    DateOnly AsOfDate,
    DateTimeOffset ImportedAt,
    Guid ImportedBy,
    int TotalRecords,
    int CreatedRecords,
    int UpdatedRecords,
    int UnchangedRecords,
    int ErrorCount,
    string Checksum,
    DataImportBatchStatus Status);
