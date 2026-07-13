using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPortalKit.Modules.Migration.LegacyContent;

namespace OpenPortalKit.Cli.Authoring;

public sealed record LegacyImportOptions(
    string InputPath,
    string OutputPath,
    string Source,
    string ImportBatch,
    DateOnly AsOfDate,
    string SchemaVersion,
    string? AssetInventoryPath);

public sealed record LegacyImportResult(
    LegacyContentMigrationReport Report,
    string OutputPath,
    int ErrorCount,
    int WarningCount);

public sealed class LegacyImportRunner
{
    public async Task<LegacyImportResult> AnalyzeAsync(
        LegacyImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var inputPath = RequireFile(options.InputPath, "Legacy CSV input");
        var outputPath = Path.GetFullPath(options.OutputPath);
        if (File.Exists(outputPath))
            throw new ArgumentException($"Output report already exists: {outputPath}");

        IReadOnlyCollection<string>? assets = null;
        if (options.AssetInventoryPath is not null)
        {
            var assetPath = RequireFile(options.AssetInventoryPath, "Asset inventory");
            assets = (await File.ReadAllLinesAsync(assetPath, cancellationToken))
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var csv = await File.ReadAllTextAsync(inputPath, cancellationToken);
        var report = new LegacyContentMigrationAnalyzer().Analyze(new LegacyContentMigrationRequest(
            options.Source,
            options.ImportBatch,
            options.AsOfDate,
            options.SchemaVersion,
            csv,
            assets));

        var parent = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        var temporaryPath = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(report, jsonOptions) + Environment.NewLine,
                cancellationToken);
            File.Move(temporaryPath, outputPath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        return new LegacyImportResult(
            report,
            outputPath,
            report.Issues.Count(issue => issue.Severity == LegacyMigrationIssueSeverity.Error),
            report.Issues.Count(issue => issue.Severity == LegacyMigrationIssueSeverity.Warning));
    }

    private static string RequireFile(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"{label} was not found: {fullPath}", fullPath);
        return fullPath;
    }
}
