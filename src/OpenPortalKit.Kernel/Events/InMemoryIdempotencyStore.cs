namespace OpenPortalKit.Kernel.Events;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _processedKeys = new(StringComparer.Ordinal);

    public Task<bool> IsProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_processedKeys.ContainsKey(idempotencyKey));
        }
    }

    public Task MarkProcessedAsync(
        string idempotencyKey,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _processedKeys[idempotencyKey] = processedAt;
        }

        return Task.CompletedTask;
    }
}
