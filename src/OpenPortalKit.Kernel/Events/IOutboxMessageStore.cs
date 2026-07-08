namespace OpenPortalKit.Kernel.Events;

public interface IOutboxMessageStore
{
    Task<OutboxMessage> AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        int maxAttemptCount,
        CancellationToken cancellationToken = default);

    Task<OutboxMessage?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid messageId,
        string lastError,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken = default);
}
