namespace OpenPortalKit.Modules.Workflow.Publishing;

public interface IApprovalRecordStore
{
    Task AddAsync(ApprovalRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalRecord>> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default);
}
