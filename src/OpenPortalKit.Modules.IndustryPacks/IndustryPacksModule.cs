using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.IndustryPacks;

public static class IndustryPacksModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "IndustryPacks",
        "vertical-adaptation",
        "Validated manifests and declarative resources for independently optional industry packs.",
        false,
        Array.Empty<string>());
}
