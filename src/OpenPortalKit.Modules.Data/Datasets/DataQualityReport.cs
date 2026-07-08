namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataQualityReport(
    Guid Id,
    Guid DataSetId,
    Guid ImportBatchId,
    int TotalRecords,
    int ErrorCount,
    IReadOnlyList<DataImportError> Errors,
    DateTimeOffset CreatedAt);
