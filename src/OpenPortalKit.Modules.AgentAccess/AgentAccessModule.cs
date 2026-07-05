using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.AgentAccess;

public static class AgentAccessModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "AgentAccess",
        "agent-seo",
        "Markdown snapshots, JSON snapshots, llms.txt, public OpenAPI, and agent readiness.",
        true,
        new[] { "Markdown", "JSON", "llms.txt", "OpenAPI" });
}
