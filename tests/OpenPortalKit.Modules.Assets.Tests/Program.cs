using OpenPortalKit.Modules.Assets.Validation;

var validator = new AssetUploadValidator(maxBytes: 1024);
var tests = new (string Name, Action Run)[]
{
    ("asset validator accepts matching safe types", AcceptsMatchingSafeTypes),
    ("asset validator rejects spoofed content", RejectsSpoofedContent),
    ("asset validator rejects unsafe names and limits", RejectsUnsafeNamesAndLimits),
    ("asset validator rejects active SVG content", RejectsSvg)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

void AcceptsMatchingSafeTypes()
{
    var png = validator.Validate("cover.png", "image/png", 100,
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
    var pdf = validator.Validate("report.pdf", "application/pdf; charset=binary", 100, "%PDF-1.7"u8);
    Assert(png.Succeeded && png.VerifiedContentType == "image/png", "Valid PNG was rejected.");
    Assert(pdf.Succeeded && pdf.VerifiedContentType == "application/pdf", "Valid PDF was rejected.");
}

void RejectsSpoofedContent()
{
    var result = validator.Validate("cover.png", "image/png", 100, "<script>"u8);
    Assert(!result.Succeeded && result.Errors.Any(error => error.Contains("signature", StringComparison.OrdinalIgnoreCase)),
        "Spoofed PNG was accepted.");
}

void RejectsUnsafeNamesAndLimits()
{
    Assert(!validator.Validate("../cover.png", "image/png", 100,
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }).Succeeded,
        "Path-shaped name was accepted.");
    Assert(!validator.Validate("cover.png", "image/png", 1025,
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }).Succeeded,
        "Oversized upload was accepted.");
}

void RejectsSvg()
{
    var result = validator.Validate("active.svg", "image/svg+xml", 100, "<svg onload='x'>"u8);
    Assert(!result.Succeeded, "SVG active content was accepted.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
