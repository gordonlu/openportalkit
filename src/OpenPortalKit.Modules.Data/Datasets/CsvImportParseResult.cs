namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record CsvImportParseResult(
    IReadOnlyList<DataImportRow> Rows,
    IReadOnlyList<DataImportError> Errors);
