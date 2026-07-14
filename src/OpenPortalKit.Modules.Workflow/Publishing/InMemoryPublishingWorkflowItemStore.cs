namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed class InMemoryPublishingWorkflowItemStore : IPublishingWorkflowItemStore
{
    private readonly object _gate = new();
    private readonly List<PublishingWorkflowItem> _items = new();

    public Task AddAsync(
        PublishingWorkflowItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_items.Any(candidate => candidate.Id == item.Id ||
                string.Equals(candidate.TargetType, item.TargetType, StringComparison.Ordinal) &&
                string.Equals(candidate.TargetId, item.TargetId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("A workflow item already exists for the target.");
            }

            _items.Add(item);
        }
        return Task.CompletedTask;
    }

    public Task<bool> TryUpdateAsync(
        PublishingWorkflowItem item,
        WorkflowState expectedState,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var index = _items.FindIndex(candidate => candidate.Id == item.Id &&
                candidate.State == expectedState && candidate.UpdatedAt == expectedUpdatedAt);
            if (index < 0) return Task.FromResult(false);
            _items[index] = item;
            return Task.FromResult(true);
        }
    }

    public Task<PublishingWorkflowItem?> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_items.FirstOrDefault(item =>
                string.Equals(item.TargetType, targetType, StringComparison.Ordinal) &&
                string.Equals(item.TargetId, targetId, StringComparison.Ordinal)));
        }
    }

    public Task<IReadOnlyList<PublishingWorkflowItem>> ListScheduledDueAsync(
        DateTimeOffset asOf,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(take));
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PublishingWorkflowItem>>(_items
                .Where(item => item.State == WorkflowState.Approved &&
                    item.ScheduledAt is not null && item.ScheduledAt <= asOf)
                .OrderBy(item => item.ScheduledAt)
                .ThenBy(item => item.Id)
                .Take(take)
                .ToArray());
        }
    }
}
