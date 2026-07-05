using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Audit;

public static class AuditModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Audit",
        "governance",
        "Queryable audit history for public-output-changing actions and admin operations.",
        true,
        new[] { "HTML", "JSON" });
}
