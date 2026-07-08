namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed record WorkflowPublicationReadiness(
    bool HasTitle,
    bool HasSlug,
    bool HasSummary);
