using System.Text.Json;
using System.Text.RegularExpressions;
using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.Cli.Authoring;

public sealed record IndustryPackScaffoldOptions(
    string Name,
    string DisplayName,
    string Description,
    string OutputDirectory);

public sealed record IndustryPackScaffoldResult(string Name, string Path);

public sealed partial class IndustryPackScaffolder
{
    public async Task<IndustryPackScaffoldResult> CreateAsync(
        IndustryPackScaffoldOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!PackNamePattern().IsMatch(options.Name))
            throw new ArgumentException("Pack name must start with an uppercase letter and contain only ASCII letters and digits.");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Description);

        var parent = Path.GetFullPath(options.OutputDirectory);
        var packPath = Path.Combine(parent, options.Name);
        if (Directory.Exists(packPath) || File.Exists(packPath))
            throw new ArgumentException($"Output path already exists: {packPath}");

        Directory.CreateDirectory(Path.Combine(packPath, "content-types"));
        var manifest = new
        {
            manifestVersion = IndustryPackContract.ManifestVersion,
            name = options.Name,
            displayName = options.DisplayName.Trim(),
            description = options.Description.Trim(),
            version = "0.1.0",
            requiresCore = IndustryPackContract.CurrentCoreVersion,
            registers = new
            {
                contentTypes = true,
                datasets = false,
                rules = false,
                templates = false,
                dashboardCards = false,
                seedData = false
            },
            resources = new
            {
                contentTypes = new[] { "content-types/catalog.json" },
                datasets = Array.Empty<string>(),
                rules = Array.Empty<string>(),
                templates = Array.Empty<string>(),
                dashboardCards = Array.Empty<string>(),
                seedData = Array.Empty<string>()
            }
        };
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(
            Path.Combine(packPath, "pack.json"),
            JsonSerializer.Serialize(manifest, jsonOptions) + Environment.NewLine,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(packPath, "content-types", "catalog.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = "opk.pack-content-types.v1",
                contentTypes = Array.Empty<object>()
            }, jsonOptions) + Environment.NewLine,
            cancellationToken);

        return new IndustryPackScaffoldResult(options.Name, packPath);
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackNamePattern();
}
