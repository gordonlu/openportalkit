namespace OpenPortalKit.Kernel.Events;

public sealed class InMemoryOutboxMessageStore : IOutboxMessageStore
{
    private readonly object _gate = new();
    private readonly List<OutboxMessage> _messages = new();

    public Task<OutboxMessage> AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.EventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.IdempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var existing = _messages.FirstOrDefault(candidate =>
                string.Equals(candidate.IdempotencyKey, message.IdempotencyKey, StringComparison.Ordinal));

            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            _messages.Add(message);
            return Task.FromResult(message);
        }
    }

    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        int maxAttemptCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttemptCount);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var pending = _messages
                .Where(message => message.ProcessedAt is null && message.AttemptCount < maxAttemptCount)
                .OrderBy(message => message.OccurredAt)
                .ThenBy(message => message.Id)
                .Take(batchSize)
                .ToArray();

            return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
        }
    }

    public Task<OutboxMessage?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_messages.FirstOrDefault(message =>
                string.Equals(message.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)));
        }
    }

    public Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            Replace(messageId, message => message with
            {
                ProcessedAt = processedAt,
                LastError = null
            });
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        Guid messageId,
        string lastError,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastError);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            Replace(messageId, message => message with
            {
                AttemptCount = message.AttemptCount + 1,
                LastError = lastError
            });
        }

        return Task.CompletedTask;
    }

    private void Replace(Guid messageId, Func<OutboxMessage, OutboxMessage> update)
    {
        var index = _messages.FindIndex(message => message.Id == messageId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Outbox message '{messageId}' was not found.");
        }

        _messages[index] = update(_messages[index]);
    }
}
