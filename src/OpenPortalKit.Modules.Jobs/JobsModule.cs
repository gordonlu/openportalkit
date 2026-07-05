using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Jobs;

public static class JobsModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Jobs",
        "operations",
        "Background job execution, retries, idempotency, and outbox processing.",
        true,
        Array.Empty<string>());
}
