using System.Text.Json;

namespace OpenPortalKit.Modules.Seo.PublicResources;

public static class SeoPageMetadataBuilder
{
    public static SeoPageMetadata Build(
        PublicResourceDescriptor resource,
        Uri siteBaseUrl,
        string siteName,
        string schemaType = "Article")
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(siteBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaType);

        var canonicalUrl = CanonicalUrlBuilder.Build(siteBaseUrl, resource.Path);
        var openGraph = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["og:title"] = resource.Title,
            ["og:description"] = resource.Description,
            ["og:type"] = "article",
            ["og:url"] = canonicalUrl.ToString(),
            ["og:site_name"] = siteName
        };

        if (!string.IsNullOrWhiteSpace(resource.Language))
        {
            openGraph["og:locale"] = resource.Language;
        }

        var jsonLd = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = schemaType,
            ["headline"] = resource.Title,
            ["description"] = resource.Description,
            ["url"] = canonicalUrl.ToString(),
            ["datePublished"] = resource.PublishedAt,
            ["dateModified"] = resource.UpdatedAt
        });

        return new SeoPageMetadata(resource.Title, resource.Description, canonicalUrl, openGraph, jsonLd);
    }
}
