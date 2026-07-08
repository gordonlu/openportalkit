namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataImportResult(
    bool Succeeded,
    bool DryRun,
    DataImportBatch Batch,
    DataQualityReport QualityReport,
    IReadOnlyList<DataRecord> Records);
