namespace OpenPortalKit.Modules.Seo.PublicResources;

public sealed record RobotsPolicy(
    Uri? SitemapUrl,
    IReadOnlyList<RobotsDirective> Directives);
