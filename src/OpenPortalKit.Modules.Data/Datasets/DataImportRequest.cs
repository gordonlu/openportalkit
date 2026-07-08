namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataImportRequest(
    Guid DataSetId,
    Guid SchemaVersionId,
    string Source,
    DateOnly AsOfDate,
    Guid ActorId,
    IReadOnlyList<DataImportRow> Rows,
    bool DryRun = false,
    string? SourceFileName = null,
    DateTimeOffset? ImportedAt = null);
