namespace OpenPortalKit.Modules.Seo.PublicResources;

public sealed record SeoPageMetadata(
    string Title,
    string Description,
    Uri CanonicalUrl,
    IReadOnlyDictionary<string, string> OpenGraph,
    string JsonLd);
