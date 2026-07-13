namespace OpenPortalKit.Modules.Migration.LegacyContent;

public sealed record LegacyContentMigrationReport(
    string Source,
    string ImportBatch,
    DateOnly AsOfDate,
    string SchemaVersion,
    string Checksum,
    DateTimeOffset AnalyzedAt,
    int TotalRows,
    int ValidRows,
    IReadOnlyList<LegacyContentCandidate> Candidates,
    IReadOnlyList<LegacyRedirectMapping> Redirects,
    IReadOnlyList<LegacyMigrationIssue> Issues)
{
    public bool CanApply => Issues.All(issue => issue.Severity != LegacyMigrationIssueSeverity.Error);
}

public sealed record LegacyContentCandidate(
    int RowNumber,
    string SourceId,
    string Title,
    string Slug,
    string Summary,
    string Body,
    IReadOnlyList<string> AssetPaths,
    string ContentChecksum);

public sealed record LegacyRedirectMapping(int RowNumber, string SourcePath, string TargetPath);

public sealed record LegacyMigrationIssue(
    int RowNumber,
    string Code,
    string Message,
    LegacyMigrationIssueSeverity Severity,
    string? SourceId = null);

public enum LegacyMigrationIssueSeverity
{
    Warning,
    Error
}
