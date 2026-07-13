namespace OpenPortalKit.Modules.Migration.LegacyContent;

public sealed record LegacyContentMigrationRequest(
    string Source,
    string ImportBatch,
    DateOnly AsOfDate,
    string SchemaVersion,
    string CsvContent,
    IReadOnlyCollection<string>? AvailableAssetPaths = null);
