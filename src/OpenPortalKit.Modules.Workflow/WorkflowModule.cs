using OpenPortalKit.Kernel.Modules;

namespace OpenPortalKit.Modules.Workflow;

public static class WorkflowModule
{
    public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
        "Workflow",
        "publishing",
        "Review, approval, scheduling, archive, and publish state transitions.",
        true,
        Array.Empty<string>());
}
