namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed record ApprovalRecord(
    Guid Id,
    Guid WorkflowItemId,
    string TargetType,
    string TargetId,
    Guid ActorId,
    WorkflowAction Action,
    WorkflowState FromState,
    WorkflowState ToState,
    string? Comment,
    DateTimeOffset OccurredAt);
