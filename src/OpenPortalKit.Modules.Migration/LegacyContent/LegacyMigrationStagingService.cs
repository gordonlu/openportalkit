using System.Text.Json;
using OpenPortalKit.Kernel.Audit;

namespace OpenPortalKit.Modules.Migration.LegacyContent;

public sealed class LegacyMigrationStagingService
{
    private readonly ILegacyMigrationBatchStore _store;
    private readonly AuditRecorder _auditRecorder;
    private readonly Func<DateTimeOffset> _clock;

    public LegacyMigrationStagingService(
        ILegacyMigrationBatchStore store,
        AuditRecorder auditRecorder,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _auditRecorder = auditRecorder;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<LegacyMigrationBatch>> ListAsync(CancellationToken cancellationToken = default) =>
        _store.ListAsync(cancellationToken);

    public Task<LegacyMigrationBatch?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        _store.FindAsync(id, cancellationToken);

    public async Task<LegacyMigrationBatch> StageAsync(
        LegacyContentMigrationReport report,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!report.CanApply) throw new InvalidOperationException("A migration report with blocking errors cannot be staged.");

        var existing = await _store.FindBySourceBatchAsync(report.Source, report.ImportBatch, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == LegacyMigrationBatchStatus.Staged &&
                string.Equals(existing.SourceChecksum, report.Checksum, StringComparison.Ordinal)) return existing;
            throw new InvalidOperationException(
                "This source and import batch is immutable. Use a new import batch for changed or rolled-back input.");
        }

        var batch = new LegacyMigrationBatch(
            Guid.NewGuid(), report.Source, report.ImportBatch, report.AsOfDate, report.SchemaVersion,
            report.Checksum, JsonSerializer.Serialize(report), report.TotalRows, report.ValidRows,
            report.Issues.Count(issue => issue.Severity == LegacyMigrationIssueSeverity.Error),
            report.Issues.Count(issue => issue.Severity == LegacyMigrationIssueSeverity.Warning),
            LegacyMigrationBatchStatus.Staged, actorId, _clock());
        await _store.AddAsync(batch, cancellationToken);
        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            actorId, "legacy-migration.staged", nameof(LegacyMigrationBatch), batch.Id.ToString(),
            $"Staged {batch.ValidRows} validated legacy content rows.",
            JsonSerializer.Serialize(new { batch.Source, batch.ImportBatch, batch.SourceChecksum, batch.SchemaVersion })),
            cancellationToken);
        return batch;
    }

    public async Task<LegacyMigrationBatch> RollbackAsync(
        Guid id,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _store.FindAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Migration batch was not found.");
        if (!await _store.MarkRolledBackAsync(id, actorId, _clock(), cancellationToken))
        {
            throw new InvalidOperationException("Only a staged migration batch can be rolled back.");
        }
        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            actorId, "legacy-migration.rolled-back", nameof(LegacyMigrationBatch), id.ToString(),
            "Rolled back a staged legacy migration batch.",
            JsonSerializer.Serialize(new { existing.Source, existing.ImportBatch, existing.SourceChecksum })),
            cancellationToken);
        return (await _store.FindAsync(id, cancellationToken))!;
    }
}
