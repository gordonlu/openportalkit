using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenPortalKit.Cli.Authoring;

public sealed record ProjectProfileDefinition(
    string Id,
    string Version,
    string DefaultSite,
    IReadOnlyList<string> SelectedIndustryPacks,
    string Checksum);

public sealed partial class ProjectProfileCatalog
{
    public const string SchemaVersion = "opk.project-template-profile.v1";
    private static readonly IReadOnlySet<string> SupportedSites =
        new HashSet<string>(["corporate", "data", "research", "activity", "finance"], StringComparer.Ordinal);

    public async Task<ProjectProfileDefinition> LoadAsync(
        string sourceRoot,
        string profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        if (string.IsNullOrWhiteSpace(profileId) || !ProfileIdPattern().IsMatch(profileId.Trim()))
            throw new ArgumentException("--profile must be a lowercase ASCII slug.");

        var normalizedId = profileId.Trim().ToLowerInvariant();
        var profileRoot = Path.Combine(Path.GetFullPath(sourceRoot), "templates", "project-profiles");
        if (!Directory.Exists(profileRoot))
            throw new ArgumentException("Template source does not contain versioned project profiles.");
        var path = Path.Combine(profileRoot, normalizedId + ".json");
        if (!File.Exists(path))
        {
            var available = Directory.EnumerateFiles(profileRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Order(StringComparer.Ordinal)
                .ToArray();
            throw new ArgumentException("--profile must be one of: " + string.Join(", ", available) + ".");
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16
        });
        var root = document.RootElement;
        RequireExactProperties(root, "schemaVersion", "id", "version", "defaultSite", "selectedIndustryPacks");
        var schemaVersion = RequiredString(root, "schemaVersion");
        var id = RequiredString(root, "id");
        var version = RequiredString(root, "version");
        var defaultSite = RequiredString(root, "defaultSite");
        if (schemaVersion != SchemaVersion)
            throw new FormatException($"Project profile '{normalizedId}' uses unsupported schema '{schemaVersion}'.");
        if (id != normalizedId || !ProfileIdPattern().IsMatch(id))
            throw new FormatException($"Project profile id '{id}' must match its file name '{normalizedId}'.");
        if (!SemanticVersionPattern().IsMatch(version))
            throw new FormatException($"Project profile '{id}' has an invalid semantic version.");
        if (!ProfileIdPattern().IsMatch(defaultSite) || !SupportedSites.Contains(defaultSite))
            throw new FormatException($"Project profile '{id}' has unsupported defaultSite '{defaultSite}'.");

        if (!root.TryGetProperty("selectedIndustryPacks", out var packsElement) ||
            packsElement.ValueKind != JsonValueKind.Array)
            throw new FormatException($"Project profile '{id}' selectedIndustryPacks must be an array.");
        var packs = packsElement.EnumerateArray().Select(element =>
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
                throw new FormatException($"Project profile '{id}' contains an invalid industry pack name.");
            return element.GetString()!;
        }).ToArray();
        if (packs.Length != packs.Distinct(StringComparer.Ordinal).Count())
            throw new FormatException($"Project profile '{id}' contains duplicate industry packs.");
        foreach (var pack in packs)
        {
            if (!PackNamePattern().IsMatch(pack) ||
                !File.Exists(Path.Combine(sourceRoot, "industry-packs", pack, "pack.json")))
                throw new FormatException($"Project profile '{id}' references missing or invalid industry pack '{pack}'.");
        }

        return new ProjectProfileDefinition(
            id,
            version,
            defaultSite,
            packs,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static void RequireExactProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException("Project profile root must be an object.");
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        var unknown = actual.Where(name => !expectedSet.Contains(name)).ToArray();
        var missing = expected.Where(name => !actual.Contains(name, StringComparer.Ordinal)).ToArray();
        if (unknown.Length > 0 || missing.Length > 0)
            throw new FormatException($"Project profile properties are invalid. Missing: [{string.Join(", ", missing)}]; unknown: [{string.Join(", ", unknown)}].");
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new FormatException($"Project profile property '{name}' must be a non-empty string.");
        return value.GetString()!;
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileIdPattern();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackNamePattern();
}
