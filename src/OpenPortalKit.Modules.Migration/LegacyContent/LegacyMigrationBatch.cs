namespace OpenPortalKit.Modules.Migration.LegacyContent;

public sealed record LegacyMigrationBatch(
    Guid Id,
    string Source,
    string ImportBatch,
    DateOnly AsOfDate,
    string SchemaVersion,
    string SourceChecksum,
    string ReportJson,
    int TotalRows,
    int ValidRows,
    int ErrorCount,
    int WarningCount,
    LegacyMigrationBatchStatus Status,
    Guid StagedBy,
    DateTimeOffset StagedAt,
    Guid? RolledBackBy = null,
    DateTimeOffset? RolledBackAt = null);

public enum LegacyMigrationBatchStatus
{
    Staged,
    RolledBack
}
