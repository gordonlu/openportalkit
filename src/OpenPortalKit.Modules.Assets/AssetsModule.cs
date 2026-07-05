using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Assets;

public static class AssetsModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Assets",
        "publishing",
        "Uploads, media metadata, validation, and public asset references.",
        true,
        new[] { "HTML", "JSON" });
}
