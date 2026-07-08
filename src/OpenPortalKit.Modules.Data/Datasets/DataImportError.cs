namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataImportError(
    int RowNumber,
    string Code,
    string Message,
    string? RecordKey = null);
