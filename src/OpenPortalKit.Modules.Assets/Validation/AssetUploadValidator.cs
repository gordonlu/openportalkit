namespace OpenPortalKit.Modules.Assets.Validation;

public sealed class AssetUploadValidator
{
    public const long DefaultMaxBytes = 25 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, AssetFileType> AllowedTypes =
        new Dictionary<string, AssetFileType>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new("image/jpeg", MatchesJpeg),
            [".jpeg"] = new("image/jpeg", MatchesJpeg),
            [".png"] = new("image/png", MatchesPng),
            [".gif"] = new("image/gif", MatchesGif),
            [".webp"] = new("image/webp", MatchesWebP),
            [".pdf"] = new("application/pdf", MatchesPdf)
        };

    private readonly long _maxBytes;

    public AssetUploadValidator(long maxBytes = DefaultMaxBytes)
    {
        if (maxBytes is < 1 or > 1024L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        _maxBytes = maxBytes;
    }

    public AssetUploadValidationResult Validate(
        string fileName,
        string declaredContentType,
        long sizeBytes,
        ReadOnlySpan<byte> signature)
    {
        var errors = new List<string>();
        var normalizedName = fileName?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 1 or > 255 ||
            normalizedName.Any(char.IsControl) ||
            normalizedName.Contains('/') || normalizedName.Contains('\\') ||
            normalizedName is "." or "..")
        {
            errors.Add("File name must be a plain name between 1 and 255 characters.");
        }

        if (sizeBytes < 1 || sizeBytes > _maxBytes)
            errors.Add($"File size must be between 1 byte and {_maxBytes} bytes.");

        var extension = Path.GetExtension(normalizedName);
        if (!AllowedTypes.TryGetValue(extension, out var expectedType))
        {
            errors.Add("File extension is not allowed.");
            return new AssetUploadValidationResult(false, normalizedName, null, errors);
        }

        var mediaType = (declaredContentType ?? string.Empty).Split(';', 2)[0].Trim();
        if (!string.Equals(mediaType, expectedType.ContentType, StringComparison.OrdinalIgnoreCase))
            errors.Add("Declared content type does not match the file extension.");
        if (!expectedType.SignatureMatches(signature))
            errors.Add("File signature does not match the declared file type.");

        return new AssetUploadValidationResult(
            errors.Count == 0,
            normalizedName,
            errors.Count == 0 ? expectedType.ContentType : null,
            errors);
    }

    private static bool MatchesJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff;

    private static bool MatchesPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });

    private static bool MatchesGif(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 6 &&
        (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8));

    private static bool MatchesWebP(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8);

    private static bool MatchesPdf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8);

    private sealed record AssetFileType(
        string ContentType,
        Func<ReadOnlySpan<byte>, bool> SignatureMatches);
}

public sealed record AssetUploadValidationResult(
    bool Succeeded,
    string FileName,
    string? VerifiedContentType,
    IReadOnlyList<string> Errors);
