namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed record PublishingWorkflowItem(
    Guid Id,
    string TargetType,
    string TargetId,
    WorkflowState State,
    int VersionNumber,
    Guid CreatedBy,
    Guid UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ReviewComment = null,
    DateTimeOffset? ApprovedAt = null,
    DateTimeOffset? PublishedAt = null,
    DateTimeOffset? ScheduledAt = null,
    DateTimeOffset? ArchivedAt = null)
{
    public static PublishingWorkflowItem CreateDraft(
        string targetType,
        string targetId,
        Guid actorId,
        DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        var now = createdAt ?? DateTimeOffset.UtcNow;

        return new PublishingWorkflowItem(
            Guid.NewGuid(),
            targetType,
            targetId,
            WorkflowState.Draft,
            VersionNumber: 1,
            actorId,
            actorId,
            now,
            now);
    }
}
