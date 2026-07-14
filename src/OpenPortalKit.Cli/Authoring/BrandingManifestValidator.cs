using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OpenPortalKit.Cli.Authoring;

public sealed record BrandingValidationError(string Code, string Path, string Message);

public sealed record BrandingValidationResult(
    BrandingManifest? Manifest,
    IReadOnlyList<BrandingValidationError> Errors,
    int AssetCount)
{
    public bool Succeeded => Errors.Count == 0;
}

public sealed class BrandingManifest
{
    public string? SchemaVersion { get; init; }
    public BrandingSite? Site { get; init; }
    public BrandingAssets? Assets { get; init; }
    public BrandingColors? Colors { get; init; }
    public BrandingTypography? Typography { get; init; }
    public List<BrandingLink>? Navigation { get; init; }
    public BrandingFooter? Footer { get; init; }
}

public sealed class BrandingSite
{
    public string? Name { get; init; }
    public string? ShortName { get; init; }
    public string? Description { get; init; }
    public string? Locale { get; init; }
}

public sealed class BrandingAssets
{
    public BrandingImage? Logo { get; init; }
    public BrandingImage? Favicon { get; init; }
    public BrandingImage? SocialImage { get; init; }
}

public sealed class BrandingImage
{
    public string? Src { get; init; }
    public string? Alt { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

public sealed class BrandingColors
{
    public string? Accent { get; init; }
    public string? AccentStrong { get; init; }
    public string? Surface { get; init; }
    public string? SurfaceMuted { get; init; }
    public string? Text { get; init; }
}

public sealed class BrandingTypography
{
    public string? Preset { get; init; }
}

public sealed class BrandingLink
{
    public string? Label { get; init; }
    public string? Href { get; init; }
}

public sealed class BrandingFooter
{
    public string? Copyright { get; init; }
    public List<BrandingLink>? Links { get; init; }
}

public sealed partial class BrandingManifestValidator
{
    public const string SchemaVersion = "opk.branding.v1";
    public const string RelativeManifestPath = "apps/web/src/lib/branding.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<BrandingValidationResult> ValidateAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        workspaceRoot = Path.GetFullPath(workspaceRoot);
        var manifestPath = Path.Combine(workspaceRoot, RelativeManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestPath))
        {
            return Failed("manifest_missing", RelativeManifestPath,
                $"Branding manifest was not found at {RelativeManifestPath}.");
        }

        BrandingManifest? manifest;
        try
        {
            await using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            manifest = await JsonSerializer.DeserializeAsync<BrandingManifest>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            return Failed("manifest_invalid_json", RelativeManifestPath,
                "Branding manifest is not valid strict JSON: " + exception.Message);
        }

        if (manifest is null)
            return Failed("manifest_empty", RelativeManifestPath, "Branding manifest must be a JSON object.");

        var errors = new List<BrandingValidationError>();
        ValidateManifest(manifest, errors);

        var assetCount = 0;
        if (manifest.Assets is not null)
        {
            if (manifest.Assets.Logo is not null)
            {
                assetCount++;
                await ValidateAssetAsync(workspaceRoot, manifest.Assets.Logo, "assets.logo", AssetRole.Logo, errors, cancellationToken);
            }

            if (manifest.Assets.Favicon is not null)
            {
                assetCount++;
                await ValidateAssetAsync(workspaceRoot, manifest.Assets.Favicon, "assets.favicon", AssetRole.Favicon, errors, cancellationToken);
            }

            if (manifest.Assets.SocialImage is not null)
            {
                assetCount++;
                await ValidateAssetAsync(workspaceRoot, manifest.Assets.SocialImage, "assets.socialImage", AssetRole.SocialImage, errors, cancellationToken);
            }
        }

        return new BrandingValidationResult(manifest, errors, assetCount);
    }

    private static void ValidateManifest(BrandingManifest manifest, ICollection<BrandingValidationError> errors)
    {
        if (manifest.SchemaVersion != SchemaVersion)
            Add(errors, "schema_version_invalid", "schemaVersion", $"schemaVersion must be '{SchemaVersion}'.");

        if (manifest.Site is null) Add(errors, "field_required", "site", "site is required.");
        else
        {
            Text(manifest.Site.Name, 2, 100, "site.name", errors);
            Text(manifest.Site.ShortName, 1, 12, "site.shortName", errors);
            Text(manifest.Site.Description, 10, 240, "site.description", errors);
            if (string.IsNullOrWhiteSpace(manifest.Site.Locale) || !LocaleRegex().IsMatch(manifest.Site.Locale))
                Add(errors, "locale_invalid", "site.locale", "locale must use a language code such as 'en' or 'zh-CN'.");
        }

        if (manifest.Assets is null) Add(errors, "field_required", "assets", "assets is required.");
        else
        {
            if (manifest.Assets.Favicon is null) Add(errors, "field_required", "assets.favicon", "favicon is required.");
            if (manifest.Assets.SocialImage is null) Add(errors, "field_required", "assets.socialImage", "socialImage is required.");
            if (manifest.Assets.Logo is not null) ImageText(manifest.Assets.Logo, "assets.logo", requireAlt: true, errors);
            if (manifest.Assets.Favicon is not null) ImageText(manifest.Assets.Favicon, "assets.favicon", requireAlt: false, errors);
            if (manifest.Assets.SocialImage is not null) ImageText(manifest.Assets.SocialImage, "assets.socialImage", requireAlt: true, errors);
        }

        if (manifest.Colors is null) Add(errors, "field_required", "colors", "colors is required.");
        else ValidateColors(manifest.Colors, errors);

        if (manifest.Typography is null) Add(errors, "field_required", "typography", "typography is required.");
        else if (manifest.Typography.Preset is not ("editorial" or "modern" or "institutional"))
            Add(errors, "typography_invalid", "typography.preset", "preset must be editorial, modern, or institutional.");

        ValidateLinks(manifest.Navigation, "navigation", 1, 8, errors);
        if (manifest.Footer is null) Add(errors, "field_required", "footer", "footer is required.");
        else
        {
            Text(manifest.Footer.Copyright, 2, 160, "footer.copyright", errors);
            ValidateLinks(manifest.Footer.Links, "footer.links", 0, 8, errors);
        }
    }

    private static void ValidateColors(BrandingColors colors, ICollection<BrandingValidationError> errors)
    {
        var values = new Dictionary<string, string?>
        {
            ["colors.accent"] = colors.Accent,
            ["colors.accentStrong"] = colors.AccentStrong,
            ["colors.surface"] = colors.Surface,
            ["colors.surfaceMuted"] = colors.SurfaceMuted,
            ["colors.text"] = colors.Text
        };
        foreach (var (path, value) in values)
            if (value is null || !ColorRegex().IsMatch(value))
                Add(errors, "color_invalid", path, "Color must use six-digit hexadecimal notation, for example #087c78.");

        if (values.Values.Any(value => value is null || !ColorRegex().IsMatch(value))) return;
        Contrast(colors.Text!, colors.Surface!, 4.5, "colors.text", "colors.surface", errors);
        Contrast(colors.Text!, colors.SurfaceMuted!, 4.5, "colors.text", "colors.surfaceMuted", errors);
        Contrast(colors.AccentStrong!, colors.Surface!, 4.5, "colors.accentStrong", "colors.surface", errors);
        Contrast("#ffffff", colors.Accent!, 4.5, "white text", "colors.accent", errors);
    }

    private static void Contrast(
        string foreground,
        string background,
        double minimum,
        string foregroundPath,
        string backgroundPath,
        ICollection<BrandingValidationError> errors)
    {
        var ratio = ContrastRatio(foreground, background);
        if (ratio < minimum)
            Add(errors, "contrast_insufficient", backgroundPath,
                $"Contrast between {foregroundPath} and {backgroundPath} is {ratio:F2}:1; at least {minimum:F1}:1 is required.");
    }

    private static void ValidateLinks(
        IReadOnlyList<BrandingLink>? links,
        string path,
        int minimum,
        int maximum,
        ICollection<BrandingValidationError> errors)
    {
        if (links is null)
        {
            Add(errors, "field_required", path, $"{path} is required.");
            return;
        }
        if (links.Count < minimum || links.Count > maximum)
            Add(errors, "link_count_invalid", path, $"{path} must contain between {minimum} and {maximum} links.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];
            var itemPath = $"{path}[{index}]";
            Text(link.Label, 1, 40, itemPath + ".label", errors);
            if (!IsSafeHref(link.Href))
                Add(errors, "link_unsafe", itemPath + ".href", "href must be a fragment, a root-relative path, or an absolute HTTPS URL.");
            else if (!seen.Add(link.Label + "\0" + link.Href))
                Add(errors, "link_duplicate", itemPath, "Duplicate links are not allowed.");
        }
    }

    private static bool IsSafeHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href) || href.Length > 300 || href.Any(char.IsControl)) return false;
        if (href.StartsWith('#')) return href.Length > 1 && !href.Contains(' ');
        if (href.StartsWith('/')) return !href.StartsWith("//", StringComparison.Ordinal) && !href.Contains('\\');
        return Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
               string.IsNullOrEmpty(uri.UserInfo);
    }

    private static void ImageText(
        BrandingImage image,
        string path,
        bool requireAlt,
        ICollection<BrandingValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(image.Src)) Add(errors, "field_required", path + ".src", "src is required.");
        if (requireAlt) Text(image.Alt, 1, 160, path + ".alt", errors);
        if (image.Width is < 1 or > 4096) Add(errors, "dimension_invalid", path + ".width", "width must be between 1 and 4096 pixels.");
        if (image.Height is < 1 or > 4096) Add(errors, "dimension_invalid", path + ".height", "height must be between 1 and 4096 pixels.");
    }

    private static async Task ValidateAssetAsync(
        string workspaceRoot,
        BrandingImage image,
        string path,
        AssetRole role,
        ICollection<BrandingValidationError> errors,
        CancellationToken cancellationToken)
    {
        if (!TryResolveAsset(workspaceRoot, image.Src, role, out var assetPath, out var reason))
        {
            Add(errors, "asset_path_unsafe", path + ".src", reason);
            return;
        }
        if (!File.Exists(assetPath))
        {
            Add(errors, "asset_missing", path + ".src", $"Asset does not exist: {image.Src}");
            return;
        }

        var file = new FileInfo(assetPath);
        var sizeLimit = role switch
        {
            AssetRole.Logo => 1024 * 1024,
            AssetRole.Favicon => 256 * 1024,
            _ => 2 * 1024 * 1024
        };
        if (file.Length == 0 || file.Length > sizeLimit)
            Add(errors, "asset_size_invalid", path + ".src", $"Asset must be non-empty and no larger than {sizeLimit / 1024} KiB.");

        try
        {
            var dimensions = await ReadDimensionsAsync(assetPath, cancellationToken);
            if (dimensions.Width != image.Width || dimensions.Height != image.Height)
                Add(errors, "asset_dimensions_mismatch", path,
                    $"Manifest declares {image.Width}x{image.Height}, but the asset is {dimensions.Width}x{dimensions.Height}.");
        }
        catch (FormatException exception)
        {
            Add(errors, "asset_invalid", path + ".src", exception.Message);
        }
    }

    private static bool TryResolveAsset(
        string workspaceRoot,
        string? source,
        AssetRole role,
        out string assetPath,
        out string reason)
    {
        assetPath = string.Empty;
        reason = "Asset src must be a root-relative local path.";
        if (string.IsNullOrWhiteSpace(source) || !source.StartsWith('/') || source.StartsWith("//", StringComparison.Ordinal) ||
            source.Contains('\\') || source.Contains('?') || source.Contains('#') || source.Any(char.IsControl)) return false;
        var segments = source.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            reason = "Asset src must not contain traversal segments.";
            return false;
        }

        var extension = Path.GetExtension(source).ToLowerInvariant();
        var allowed = role switch
        {
            AssetRole.Logo => new[] { ".svg", ".png", ".webp", ".jpg", ".jpeg" },
            AssetRole.Favicon => new[] { ".ico", ".png", ".svg" },
            _ => new[] { ".png", ".webp", ".jpg", ".jpeg" }
        };
        if (!allowed.Contains(extension, StringComparer.Ordinal))
        {
            reason = $"The {role.ToString().ToLowerInvariant()} asset type '{extension}' is not allowed.";
            return false;
        }

        var basePath = source == "/favicon.ico"
            ? Path.Combine(workspaceRoot, "apps", "web", "src", "app")
            : Path.Combine(workspaceRoot, "apps", "web", "public");
        var relative = source == "/favicon.ico" ? "favicon.ico" : string.Join(Path.DirectorySeparatorChar, segments);
        assetPath = Path.GetFullPath(Path.Combine(basePath, relative));
        var boundary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(basePath)) + Path.DirectorySeparatorChar;
        if (!assetPath.StartsWith(boundary, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            reason = "Asset resolves outside the allowed Web asset directory.";
            return false;
        }
        return true;
    }

    private static async Task<(int Width, int Height)> ReadDimensionsAsync(string path, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".svg") return ReadSvgDimensions(path);
        var bytes = new byte[Math.Min(new FileInfo(path).Length, 1024 * 1024)];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = await stream.ReadAtLeastAsync(bytes, Math.Min(bytes.Length, 32), throwOnEndOfStream: false, cancellationToken);
        var data = bytes.AsSpan(0, length);
        return extension switch
        {
            ".png" => ReadPng(data),
            ".webp" => ReadWebP(data),
            ".jpg" or ".jpeg" => ReadJpeg(data),
            ".ico" => ReadIco(data),
            _ => throw new FormatException("Unsupported image format.")
        };
    }

    private static (int Width, int Height) ReadPng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || !data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new FormatException("PNG signature is invalid.");
        return (BinaryPrimitives.ReadInt32BigEndian(data[16..20]), BinaryPrimitives.ReadInt32BigEndian(data[20..24]));
    }

    private static (int Width, int Height) ReadWebP(ReadOnlySpan<byte> data)
    {
        if (data.Length < 30 || !data[..4].SequenceEqual("RIFF"u8) || !data[8..12].SequenceEqual("WEBP"u8))
            throw new FormatException("WebP signature is invalid.");
        var kind = data[12..16];
        if (kind.SequenceEqual("VP8 "u8))
            return (BinaryPrimitives.ReadUInt16LittleEndian(data[26..28]) & 0x3fff,
                BinaryPrimitives.ReadUInt16LittleEndian(data[28..30]) & 0x3fff);
        if (kind.SequenceEqual("VP8X"u8))
            return (1 + data[24] + (data[25] << 8) + (data[26] << 16),
                1 + data[27] + (data[28] << 8) + (data[29] << 16));
        if (kind.SequenceEqual("VP8L"u8) && data.Length >= 25 && data[20] == 0x2f)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..25]);
            return ((int)(bits & 0x3fff) + 1, (int)((bits >> 14) & 0x3fff) + 1);
        }
        throw new FormatException("WebP encoding is unsupported or malformed.");
    }

    private static (int Width, int Height) ReadJpeg(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xff || data[1] != 0xd8) throw new FormatException("JPEG signature is invalid.");
        var offset = 2;
        while (offset + 9 < data.Length)
        {
            if (data[offset++] != 0xff) continue;
            while (offset < data.Length && data[offset] == 0xff) offset++;
            if (offset >= data.Length) break;
            var marker = data[offset++];
            if (marker is 0xd8 or 0xd9 || marker is >= 0xd0 and <= 0xd7) continue;
            if (offset + 2 > data.Length) break;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data[offset..(offset + 2)]);
            if (segmentLength < 2 || offset + segmentLength > data.Length) break;
            if (marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
                return (BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 5)..(offset + 7)]),
                    BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 3)..(offset + 5)]));
            offset += segmentLength;
        }
        throw new FormatException("JPEG dimensions could not be read.");
    }

    private static (int Width, int Height) ReadIco(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(data[..2]) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data[2..4]) != 1) throw new FormatException("ICO signature is invalid.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data[4..6]);
        if (count == 0 || data.Length < 6 + count * 16) throw new FormatException("ICO directory is invalid.");
        var width = 0;
        var height = 0;
        for (var index = 0; index < count; index++)
        {
            var offset = 6 + index * 16;
            width = Math.Max(width, data[offset] == 0 ? 256 : data[offset]);
            height = Math.Max(height, data[offset + 1] == 0 ? 256 : data[offset + 1]);
        }
        return (width, height);
    }

    private static (int Width, int Height) ReadSvgDimensions(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        var root = document.Root;
        if (root is null || root.Name.LocalName != "svg") throw new FormatException("SVG root element is invalid.");
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.LocalName is "script" or "foreignObject" or "style") throw new FormatException("SVG active content is not allowed.");
            foreach (var attribute in element.Attributes())
            {
                var name = attribute.Name.LocalName;
                var value = attribute.Value.Trim();
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || value.Contains("url(", StringComparison.OrdinalIgnoreCase) ||
                    (name is "href" or "src" && !value.StartsWith('#')))
                    throw new FormatException("SVG event handlers and external references are not allowed.");
            }
        }
        if (TrySvgLength(root.Attribute("width")?.Value, out var width) &&
            TrySvgLength(root.Attribute("height")?.Value, out var height)) return (width, height);
        var viewBox = root.Attribute("viewBox")?.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (viewBox is { Length: 4 } && double.TryParse(viewBox[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var boxWidth) &&
            double.TryParse(viewBox[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var boxHeight) && boxWidth > 0 && boxHeight > 0)
            return ((int)Math.Round(boxWidth), (int)Math.Round(boxHeight));
        throw new FormatException("SVG must declare positive pixel dimensions or a viewBox.");
    }

    private static bool TrySvgLength(string? value, out int result)
    {
        result = 0;
        if (value?.EndsWith("px", StringComparison.OrdinalIgnoreCase) == true) value = value[..^2];
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 &&
               parsed <= 4096 && (result = (int)Math.Round(parsed)) > 0;
    }

    private static void Text(string? value, int minimum, int maximum, string path, ICollection<BrandingValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minimum || value.Trim().Length > maximum || value.Any(char.IsControl))
            Add(errors, "text_invalid", path, $"Value must contain {minimum} to {maximum} visible characters.");
    }

    private static double ContrastRatio(string first, string second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double Luminance(string color)
    {
        static double Channel(int value)
        {
            var normalized = value / 255d;
            return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(Convert.ToInt32(color.Substring(1, 2), 16)) +
               0.7152 * Channel(Convert.ToInt32(color.Substring(3, 2), 16)) +
               0.0722 * Channel(Convert.ToInt32(color.Substring(5, 2), 16));
    }

    private static BrandingValidationResult Failed(string code, string path, string message) =>
        new(null, [new BrandingValidationError(code, path, message)], 0);

    private static void Add(ICollection<BrandingValidationError> errors, string code, string path, string message) =>
        errors.Add(new BrandingValidationError(code, path, message));

    [GeneratedRegex("^[a-z]{2}(?:-[A-Z]{2})?$")]
    private static partial Regex LocaleRegex();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorRegex();

    private enum AssetRole { Logo, Favicon, SocialImage }
}
