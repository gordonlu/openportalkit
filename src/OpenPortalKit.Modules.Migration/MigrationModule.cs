using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Migration;

public static class MigrationModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Migration",
        "platform",
        "Dry-run analysis and controlled import contracts for legacy publishing systems.",
        true,
        Array.Empty<string>());
}
