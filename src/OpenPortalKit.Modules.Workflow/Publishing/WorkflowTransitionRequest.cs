namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed record WorkflowTransitionRequest(
    WorkflowAction Action,
    Guid ActorId,
    string? Comment = null,
    DateTimeOffset? ScheduledAt = null,
    DateTimeOffset? OccurredAt = null,
    WorkflowPublicationReadiness? Readiness = null);
