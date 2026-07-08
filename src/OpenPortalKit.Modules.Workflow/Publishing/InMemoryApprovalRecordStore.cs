namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed class InMemoryApprovalRecordStore : IApprovalRecordStore
{
    private readonly List<ApprovalRecord> _records = new();
    private readonly object _syncRoot = new();

    public Task AddAsync(ApprovalRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ApprovalRecord>> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<ApprovalRecord>>(
                _records
                    .Where(record =>
                        string.Equals(record.TargetType, targetType, StringComparison.Ordinal) &&
                        string.Equals(record.TargetId, targetId, StringComparison.Ordinal))
                    .OrderByDescending(record => record.OccurredAt)
                    .ToArray());
        }
    }
}
