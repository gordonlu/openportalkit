using System.Text.RegularExpressions;

namespace OpenPortalKit.Modules.Seo.PublicResources;

public static partial class CanonicalUrlBuilder
{
    public static Uri Build(Uri siteBaseUrl, string path)
    {
        ArgumentNullException.ThrowIfNull(siteBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!siteBaseUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Site base URL must be absolute.", nameof(siteBaseUrl));
        }

        var normalizedPath = NormalizePath(path);
        return new Uri(siteBaseUrl.GetLeftPart(UriPartial.Authority) + normalizedPath, UriKind.Absolute);
    }

    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var trimmed = path.Trim();
        var queryStart = trimmed.IndexOfAny(['?', '#']);

        if (queryStart >= 0)
        {
            trimmed = trimmed[..queryStart];
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            trimmed = absoluteUri.AbsolutePath;
        }

        trimmed = SlashRegex().Replace(trimmed, "/").TrimEnd('/');

        if (trimmed.Length == 0)
        {
            return "/";
        }

        return trimmed[0] == '/' ? trimmed : "/" + trimmed;
    }

    [GeneratedRegex("/{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex SlashRegex();
}
