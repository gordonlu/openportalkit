using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Dashboard;

public static class DashboardModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Dashboard",
        "operations",
        "Aggregated publishing, data, search, job, and agent-readiness health signals.",
        false,
        new[] { "HTML", "JSON" });
}
