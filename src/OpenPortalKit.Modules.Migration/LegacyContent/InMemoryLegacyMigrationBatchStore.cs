namespace OpenPortalKit.Modules.Migration.LegacyContent;

public sealed class InMemoryLegacyMigrationBatchStore : ILegacyMigrationBatchStore
{
    private readonly object _gate = new();
    private readonly List<LegacyMigrationBatch> _batches = [];

    public Task<IReadOnlyList<LegacyMigrationBatch>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<LegacyMigrationBatch>>(
                _batches.OrderByDescending(batch => batch.StagedAt).ToArray());
        }
    }

    public Task<LegacyMigrationBatch?> FindBySourceBatchAsync(
        string source,
        string importBatch,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_batches.SingleOrDefault(batch =>
                string.Equals(batch.Source, source, StringComparison.Ordinal) &&
                string.Equals(batch.ImportBatch, importBatch, StringComparison.Ordinal)));
        }
    }

    public Task<LegacyMigrationBatch?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_batches.SingleOrDefault(batch => batch.Id == id));
        }
    }

    public Task AddAsync(LegacyMigrationBatch batch, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_batches.Any(item => item.Source == batch.Source && item.ImportBatch == batch.ImportBatch))
            {
                throw new InvalidOperationException("A migration batch with this source and import batch already exists.");
            }
            _batches.Add(batch);
        }
        return Task.CompletedTask;
    }

    public Task<bool> MarkRolledBackAsync(
        Guid id,
        Guid actorId,
        DateTimeOffset rolledBackAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var index = _batches.FindIndex(batch => batch.Id == id && batch.Status == LegacyMigrationBatchStatus.Staged);
            if (index < 0) return Task.FromResult(false);
            _batches[index] = _batches[index] with
            {
                Status = LegacyMigrationBatchStatus.RolledBack,
                RolledBackBy = actorId,
                RolledBackAt = rolledBackAt
            };
            return Task.FromResult(true);
        }
    }
}
