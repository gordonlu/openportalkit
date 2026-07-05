using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Search;

public static class SearchModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Search",
        "discovery",
        "Search document contracts, providers, indexing events, and reindex jobs.",
        true,
        new[] { "HTML", "JSON" });
}
