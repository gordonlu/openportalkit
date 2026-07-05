using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Content;

public static class ContentModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Content",
        "publishing",
        "Content types, content items, versions, taxonomies, and publishing state.",
        true,
        new[] { "HTML", "Markdown", "JSON", "RSS", "Sitemap" });
}
