namespace OpenPortalKit.Kernel.Audit;

public sealed class InMemoryAuditLogStore : IAuditLogStore
{
    private readonly object _gate = new();
    private readonly List<AuditLog> _logs = new();

    public Task<AuditLog> AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _logs.Add(log);
        }

        return Task.FromResult(log);
    }

    public Task<IReadOnlyList<AuditLog>> FindByActorAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var results = _logs
                .Where(log => log.ActorId == actorId)
                .OrderByDescending(log => log.OccurredAt)
                .ToArray();

            return Task.FromResult<IReadOnlyList<AuditLog>>(results);
        }
    }

    public Task<IReadOnlyList<AuditLog>> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var results = _logs
                .Where(log =>
                    string.Equals(log.TargetType, targetType, StringComparison.Ordinal) &&
                    string.Equals(log.TargetId, targetId, StringComparison.Ordinal))
                .OrderByDescending(log => log.OccurredAt)
                .ToArray();

            return Task.FromResult<IReadOnlyList<AuditLog>>(results);
        }
    }
}
