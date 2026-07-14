namespace OpenPortalKit.Modules.Workflow.Publishing;

public interface IPublishingWorkflowItemStore
{
    Task AddAsync(
        PublishingWorkflowItem item,
        CancellationToken cancellationToken = default);

    Task<bool> TryUpdateAsync(
        PublishingWorkflowItem item,
        WorkflowState expectedState,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default);

    Task<PublishingWorkflowItem?> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublishingWorkflowItem>> ListScheduledDueAsync(
        DateTimeOffset asOf,
        int take,
        CancellationToken cancellationToken = default);
}
