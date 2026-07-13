using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenPortalKit.Modules.Content.ContentItems;
using OpenPortalKit.Modules.Data.Datasets;
using OpenPortalKit.Modules.Seo.PublicResources;

namespace OpenPortalKit.Modules.Migration.LegacyContent;

public sealed class LegacyContentMigrationAnalyzer
{
    private readonly Func<DateTimeOffset> _clock;

    public LegacyContentMigrationAnalyzer(Func<DateTimeOffset>? clock = null) =>
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public LegacyContentMigrationReport Analyze(LegacyContentMigrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ImportBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SchemaVersion);

        var parsed = CsvImportParser.Parse(request.CsvContent, "source_id");
        var issues = parsed.Errors.Select(error => new LegacyMigrationIssue(
            error.RowNumber, error.Code, error.Message, LegacyMigrationIssueSeverity.Error, error.RecordKey)).ToList();
        var candidates = new List<LegacyContentCandidate>();
        var redirects = new List<LegacyRedirectMapping>();
        var availableAssets = new HashSet<string>(
            request.AvailableAssetPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < parsed.Rows.Count; index++)
        {
            var row = parsed.Rows[index];
            var rowNumber = row.SourceRowNumber ?? index + 2;
            var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(row.PayloadJson)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            fields = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
            var sourceId = row.RecordKey.Trim();
            var title = Get(fields, "title");
            var suppliedSlug = Get(fields, "slug");
            var summary = Get(fields, "summary");
            var body = Get(fields, "body");
            var oldUrl = Get(fields, "old_url");
            var assetPaths = Get(fields, "asset_paths")
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            ValidateRequired(sourceId, "source_id", rowNumber);
            ValidateRequired(title, "title", rowNumber);
            ValidateRequired(suppliedSlug, "slug", rowNumber);
            ValidateRequired(summary, "summary", rowNumber);
            ValidateRequired(body, "body", rowNumber);

            var normalizedSlug = SlugGenerator.Generate(suppliedSlug);
            if (!string.Equals(normalizedSlug, suppliedSlug, StringComparison.Ordinal))
            {
                issues.Add(new LegacyMigrationIssue(rowNumber, "slug_not_canonical",
                    $"Slug must be canonical; suggested value is '{normalizedSlug}'.",
                    LegacyMigrationIssueSeverity.Error, sourceId));
            }
            foreach (var assetPath in assetPaths.Where(path => !availableAssets.Contains(path)))
            {
                issues.Add(new LegacyMigrationIssue(rowNumber, "asset_missing",
                    $"Asset '{assetPath}' is not present in the supplied asset inventory.",
                    LegacyMigrationIssueSeverity.Error, sourceId));
            }

            if (!string.IsNullOrWhiteSpace(oldUrl))
            {
                var sourcePath = CanonicalUrlBuilder.NormalizePath(oldUrl);
                var targetPath = "/content/" + normalizedSlug;
                if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new LegacyMigrationIssue(rowNumber, "redirect_loop",
                        "Old URL resolves to the new content URL.", LegacyMigrationIssueSeverity.Error, sourceId));
                }
                else
                {
                    redirects.Add(new LegacyRedirectMapping(rowNumber, sourcePath, targetPath));
                }
            }

            candidates.Add(new LegacyContentCandidate(
                rowNumber, sourceId, title, normalizedSlug, summary, body, assetPaths,
                Checksum(string.Join('\n', title.Trim(), summary.Trim(), body.Trim()))));

            void ValidateRequired(string value, string field, int currentRow)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    issues.Add(new LegacyMigrationIssue(currentRow, "required_field_missing",
                        $"Required field '{field}' is empty.", LegacyMigrationIssueSeverity.Error, sourceId));
                }
            }
        }

        AddDuplicateIssues(candidates.GroupBy(item => item.SourceId, StringComparer.OrdinalIgnoreCase), "source_id_duplicate", "source ID", issues);
        AddDuplicateIssues(candidates.GroupBy(item => item.Slug, StringComparer.OrdinalIgnoreCase), "slug_duplicate", "slug", issues);
        AddDuplicateIssues(redirects.GroupBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase), "old_url_duplicate", "old URL", issues);
        AddDuplicateContentWarnings(candidates, issues);

        var errorRows = issues.Where(issue => issue.Severity == LegacyMigrationIssueSeverity.Error)
            .Select(issue => issue.RowNumber).ToHashSet();
        return new LegacyContentMigrationReport(
            request.Source.Trim(), request.ImportBatch.Trim(), request.AsOfDate, request.SchemaVersion.Trim(),
            Checksum(request.CsvContent), _clock(),
            parsed.Rows.Count + parsed.Errors.Count(error => error.RowNumber > 1),
            candidates.Count(candidate => !errorRows.Contains(candidate.RowNumber)),
            candidates, redirects, issues.OrderBy(issue => issue.RowNumber).ThenBy(issue => issue.Code).ToArray());
    }

    private static string Get(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string Checksum(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void AddDuplicateIssues<T>(
        IEnumerable<IGrouping<string, T>> groups,
        string code,
        string label,
        ICollection<LegacyMigrationIssue> issues) where T : notnull
    {
        foreach (var group in groups.Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
        {
            foreach (var item in group)
            {
                var rowNumber = item switch
                {
                    LegacyContentCandidate candidate => candidate.RowNumber,
                    LegacyRedirectMapping redirect => redirect.RowNumber,
                    _ => 0
                };
                issues.Add(new LegacyMigrationIssue(rowNumber, code,
                    $"Duplicate {label} '{group.Key}'.", LegacyMigrationIssueSeverity.Error));
            }
        }
    }

    private static void AddDuplicateContentWarnings(
        IReadOnlyList<LegacyContentCandidate> candidates,
        ICollection<LegacyMigrationIssue> issues)
    {
        foreach (var group in candidates.GroupBy(item => item.ContentChecksum).Where(group => group.Count() > 1))
        {
            var sourceIds = string.Join(", ", group.Select(item => item.SourceId));
            foreach (var item in group)
            {
                issues.Add(new LegacyMigrationIssue(item.RowNumber, "duplicate_content",
                    $"Content matches source records: {sourceIds}.", LegacyMigrationIssueSeverity.Warning, item.SourceId));
            }
        }
    }
}
