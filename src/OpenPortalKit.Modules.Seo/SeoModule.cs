using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Seo;

public static class SeoModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "SEO",
        "public-rendering",
        "Metadata, canonical URLs, JSON-LD, sitemap, RSS, robots, and redirects.",
        true,
        new[] { "Sitemap", "RSS", "robots.txt", "JSON-LD" });
}
