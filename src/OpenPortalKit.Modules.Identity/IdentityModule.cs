using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Identity;

public static class IdentityModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Identity",
        "platform",
        "Users, roles, permissions, authentication, and authorization boundaries.",
        true,
        Array.Empty<string>());
}
