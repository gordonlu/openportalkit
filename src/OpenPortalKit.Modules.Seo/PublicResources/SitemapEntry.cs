namespace OpenPortalKit.Modules.Seo.PublicResources;

public sealed record SitemapEntry(
    Uri Location,
    DateTimeOffset LastModified,
    SitemapChangeFrequency ChangeFrequency = SitemapChangeFrequency.Weekly,
    decimal Priority = 0.5m);
