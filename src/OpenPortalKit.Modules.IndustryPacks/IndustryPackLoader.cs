using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenPortalKit.Modules.IndustryPacks;

public sealed partial class IndustryPackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Version _coreVersion;

    private static readonly IReadOnlySet<string> ManifestProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "manifestVersion", "name", "displayName", "description", "version", "requiresCore", "registers", "resources"
    };

    private static readonly IReadOnlySet<string> RegistrationProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "contentTypes", "datasets", "rules", "templates", "dashboardCards", "seedData"
    };

    public IndustryPackLoader(string coreVersion)
    {
        if (!Version.TryParse(coreVersion, out var version))
        {
            throw new ArgumentException("Core version must use numeric semantic version format.", nameof(coreVersion));
        }

        _coreVersion = version;
    }

    public async Task<IndustryPackLoadResult> LoadAsync(
        string packRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packRoot);
        var errors = new List<IndustryPackValidationError>();
        var root = Path.GetFullPath(packRoot);
        var manifestPath = Path.Combine(root, "pack.json");
        if (!File.Exists(manifestPath))
        {
            return Failed("manifest_missing", "Industry pack must contain pack.json.", "pack.json");
        }

        string manifestJson;
        IndustryPackManifest? manifest;
        try
        {
            manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            using var manifestDocument = JsonDocument.Parse(manifestJson);
            if (manifestDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failed("manifest_root_invalid", "pack.json must use a JSON object root.", "pack.json");
            }

            ValidateObjectProperties(manifestDocument.RootElement, ManifestProperties, "pack.json", errors);
            ValidateNestedObject(manifestDocument.RootElement, "registers", RegistrationProperties, errors);
            ValidateNestedObject(manifestDocument.RootElement, "resources", RegistrationProperties, errors);
            if (errors.Count > 0)
            {
                return new IndustryPackLoadResult(null, errors);
            }

            manifest = JsonSerializer.Deserialize<IndustryPackManifest>(manifestJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            return Failed("manifest_invalid_json", exception.Message, "pack.json");
        }

        if (manifest is null)
        {
            return Failed("manifest_empty", "pack.json did not contain a manifest object.", "pack.json");
        }

        ValidateManifest(manifest, errors);
        if (manifest.Registers is null || manifest.Resources is null)
        {
            return new IndustryPackLoadResult(null, errors);
        }
        var resources = new List<IndustryPackResource>();
        var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in manifest.Resources.Enumerate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = declaration.Path.Replace('\\', '/').Trim();
            if (!declaredPaths.Add(relativePath))
            {
                errors.Add(new IndustryPackValidationError(
                    "resource_duplicate",
                    $"Resource '{relativePath}' is declared more than once.",
                    relativePath));
                continue;
            }

            if (!IsSafeJsonPath(root, relativePath, out var fullPath))
            {
                errors.Add(new IndustryPackValidationError(
                    "resource_path_invalid",
                    "Resource paths must be relative JSON paths contained by the pack directory.",
                    relativePath));
                continue;
            }

            if (!File.Exists(fullPath))
            {
                errors.Add(new IndustryPackValidationError(
                    "resource_missing",
                    $"Declared resource '{relativePath}' does not exist.",
                    relativePath));
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new IndustryPackValidationError(
                        "resource_root_invalid",
                        "Pack resources must use a JSON object root.",
                        relativePath));
                    continue;
                }

                resources.Add(new IndustryPackResource(
                    declaration.Kind,
                    relativePath,
                    ComputeChecksum(json),
                    document.RootElement.Clone()));
            }
            catch (JsonException exception)
            {
                errors.Add(new IndustryPackValidationError(
                    "resource_invalid_json",
                    exception.Message,
                    relativePath));
            }
        }

        ValidateRegistrationCoverage(manifest, resources, errors);
        return errors.Count > 0
            ? new IndustryPackLoadResult(null, errors)
            : new IndustryPackLoadResult(
                new LoadedIndustryPack(manifest, root, ComputeChecksum(manifestJson), resources),
                Array.Empty<IndustryPackValidationError>());
    }

    private void ValidateManifest(IndustryPackManifest manifest, ICollection<IndustryPackValidationError> errors)
    {
        if (!string.Equals(manifest.ManifestVersion, IndustryPackContract.ManifestVersion, StringComparison.Ordinal))
        {
            errors.Add(new IndustryPackValidationError(
                "manifest_schema_version_unsupported",
                $"manifestVersion must be '{IndustryPackContract.ManifestVersion}'.",
                "pack.json"));
        }

        if (!PackNamePattern().IsMatch(manifest.Name))
        {
            errors.Add(new IndustryPackValidationError(
                "manifest_name_invalid",
                "Pack name must start with an uppercase letter and contain only ASCII letters and digits.",
                "pack.json"));
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            errors.Add(new IndustryPackValidationError("manifest_display_name_required", "Display name is required.", "pack.json"));
        }

        if (string.IsNullOrWhiteSpace(manifest.Description))
        {
            errors.Add(new IndustryPackValidationError("manifest_description_required", "Description is required.", "pack.json"));
        }

        if (!TryParseThreePartVersion(manifest.Version, out _))
        {
            errors.Add(new IndustryPackValidationError("manifest_version_invalid", "Pack version must use major.minor.patch format.", "pack.json"));
        }

        if (!TryParseThreePartVersion(manifest.RequiresCore, out var requiredCore))
        {
            errors.Add(new IndustryPackValidationError("manifest_core_version_invalid", "requiresCore must use major.minor.patch format.", "pack.json"));
        }
        else if (requiredCore > _coreVersion)
        {
            errors.Add(new IndustryPackValidationError(
                "manifest_core_version_unsupported",
                $"Pack requires core {requiredCore}, but the host provides {_coreVersion}.",
                "pack.json"));
        }

        if (manifest.Registers is null)
        {
            errors.Add(new IndustryPackValidationError("manifest_registers_required", "registers is required.", "pack.json"));
        }

        if (manifest.Resources is null)
        {
            errors.Add(new IndustryPackValidationError("manifest_resources_required", "resources is required.", "pack.json"));
        }

        if (manifest.Registers is not null && !new[]
            {
                manifest.Registers.ContentTypes,
                manifest.Registers.Datasets,
                manifest.Registers.Rules,
                manifest.Registers.Templates,
                manifest.Registers.DashboardCards,
                manifest.Registers.SeedData
            }.Any(enabled => enabled))
        {
            errors.Add(new IndustryPackValidationError(
                "manifest_registration_required",
                "At least one registration must be enabled.",
                "pack.json#/registers"));
        }
    }

    private static void ValidateRegistrationCoverage(
        IndustryPackManifest manifest,
        IReadOnlyCollection<IndustryPackResource> resources,
        ICollection<IndustryPackValidationError> errors)
    {
        var coverage = new[]
        {
            (manifest.Registers.ContentTypes, IndustryPackResourceKind.ContentType, "contentTypes"),
            (manifest.Registers.Datasets, IndustryPackResourceKind.DataSet, "datasets"),
            (manifest.Registers.Rules, IndustryPackResourceKind.Rule, "rules"),
            (manifest.Registers.Templates, IndustryPackResourceKind.Template, "templates"),
            (manifest.Registers.DashboardCards, IndustryPackResourceKind.DashboardCard, "dashboardCards"),
            (manifest.Registers.SeedData, IndustryPackResourceKind.SeedData, "seedData")
        };

        foreach (var (enabled, kind, name) in coverage)
        {
            var count = resources.Count(resource => resource.Kind == kind);
            if (enabled && count == 0)
            {
                errors.Add(new IndustryPackValidationError(
                    "registration_resource_missing",
                    $"Registration '{name}' is enabled but no valid resource is declared.",
                    "pack.json"));
            }
            else if (!enabled && count > 0)
            {
                errors.Add(new IndustryPackValidationError(
                    "registration_resource_disabled",
                    $"Resources for '{name}' are declared but the registration is disabled.",
                    "pack.json"));
            }
        }
    }

    private static void ValidateNestedObject(
        JsonElement root,
        string propertyName,
        IReadOnlySet<string> properties,
        ICollection<IndustryPackValidationError> errors)
    {
        if (!root.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new IndustryPackValidationError(
                "manifest_property_invalid",
                $"'{propertyName}' must be a JSON object.",
                $"pack.json#/{propertyName}"));
            return;
        }

        ValidateObjectProperties(nested, properties, $"pack.json#/{propertyName}", errors);
    }

    private static void ValidateObjectProperties(
        JsonElement element,
        IReadOnlySet<string> properties,
        string path,
        ICollection<IndustryPackValidationError> errors)
    {
        var actual = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var unknown = actual.Except(properties, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var missing = properties.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            errors.Add(new IndustryPackValidationError(
                "manifest_property_unknown",
                "Unknown properties: " + string.Join(", ", unknown),
                path));
        }

        if (missing.Length > 0)
        {
            errors.Add(new IndustryPackValidationError(
                "manifest_property_missing",
                "Required properties are missing: " + string.Join(", ", missing),
                path));
        }
    }

    private static bool IsSafeJsonPath(string root, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            !string.Equals(Path.GetExtension(relativePath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootedPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootedPrefix, StringComparison.Ordinal);
    }

    private static bool TryParseThreePartVersion(string value, out Version version)
    {
        return Version.TryParse(value, out version!) && version.Build >= 0 && version.Revision < 0;
    }

    private static string ComputeChecksum(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static IndustryPackLoadResult Failed(string code, string message, string? path)
    {
        return new IndustryPackLoadResult(null, new[] { new IndustryPackValidationError(code, message, path) });
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackNamePattern();
}
