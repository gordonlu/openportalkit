namespace OpenPortalKit.Kernel.Audit;

public interface IAuditLogStore
{
    Task<AuditLog> AddAsync(AuditLog log, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> FindByActorAsync(Guid actorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default);
}
