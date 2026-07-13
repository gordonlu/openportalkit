namespace OpenPortalKit.Modules.Migration.LegacyContent;

public interface ILegacyMigrationBatchStore
{
    Task<IReadOnlyList<LegacyMigrationBatch>> ListAsync(CancellationToken cancellationToken = default);
    Task<LegacyMigrationBatch?> FindBySourceBatchAsync(
        string source,
        string importBatch,
        CancellationToken cancellationToken = default);
    Task<LegacyMigrationBatch?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(LegacyMigrationBatch batch, CancellationToken cancellationToken = default);
    Task<bool> MarkRolledBackAsync(
        Guid id,
        Guid actorId,
        DateTimeOffset rolledBackAt,
        CancellationToken cancellationToken = default);
}
