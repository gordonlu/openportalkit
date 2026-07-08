namespace OpenPortalKit.Kernel.Events;

public interface IIdempotencyStore
{
    Task<bool> IsProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(string idempotencyKey, DateTimeOffset processedAt, CancellationToken cancellationToken = default);
}
