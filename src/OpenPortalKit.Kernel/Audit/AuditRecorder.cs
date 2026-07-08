namespace OpenPortalKit.Kernel.Audit;

public sealed class AuditRecorder
{
    private readonly IAuditLogStore _store;

    public AuditRecorder(IAuditLogStore store)
    {
        _store = store;
    }

    public Task<AuditLog> RecordAsync(
        AuditRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetId);

        var log = new AuditLog(
            Guid.NewGuid(),
            request.ActorId,
            request.Action,
            request.TargetType,
            request.TargetId,
            request.Summary,
            request.MetadataJson,
            DateTimeOffset.UtcNow);

        return _store.AddAsync(log, cancellationToken);
    }
}
