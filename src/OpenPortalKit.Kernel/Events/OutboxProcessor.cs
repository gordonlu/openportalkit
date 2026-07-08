namespace OpenPortalKit.Kernel.Events;

public sealed class OutboxProcessor
{
    private readonly IOutboxMessageStore _messageStore;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IReadOnlyDictionary<string, IOutboxMessageHandler> _handlers;
    private readonly RetryPolicy _retryPolicy;

    public OutboxProcessor(
        IOutboxMessageStore messageStore,
        IIdempotencyStore idempotencyStore,
        IEnumerable<IOutboxMessageHandler> handlers,
        RetryPolicy? retryPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        _messageStore = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _retryPolicy = retryPolicy ?? RetryPolicy.Default;
        _handlers = handlers.ToDictionary(handler => handler.EventName, StringComparer.Ordinal);
    }

    public async Task<OutboxProcessingResult> ProcessPendingAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var processed = 0;
        var failed = 0;
        var skipped = 0;

        var messages = await _messageStore.GetPendingAsync(
            batchSize,
            _retryPolicy.MaxAttemptCount,
            cancellationToken);

        foreach (var message in messages)
        {
            if (await _idempotencyStore.IsProcessedAsync(message.IdempotencyKey, cancellationToken))
            {
                await _messageStore.MarkProcessedAsync(message.Id, DateTimeOffset.UtcNow, cancellationToken);
                skipped++;
                continue;
            }

            if (!_handlers.TryGetValue(message.EventName, out var handler))
            {
                await _messageStore.MarkFailedAsync(
                    message.Id,
                    $"No outbox handler is registered for event '{message.EventName}'.",
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                failed++;
                continue;
            }

            try
            {
                await handler.HandleAsync(message, cancellationToken);
                var processedAt = DateTimeOffset.UtcNow;
                await _idempotencyStore.MarkProcessedAsync(message.IdempotencyKey, processedAt, cancellationToken);
                await _messageStore.MarkProcessedAsync(message.Id, processedAt, cancellationToken);
                processed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await _messageStore.MarkFailedAsync(
                    message.Id,
                    string.IsNullOrWhiteSpace(exception.Message)
                        ? exception.GetType().Name
                        : exception.Message,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                failed++;
            }
        }

        return new OutboxProcessingResult(processed, failed, skipped);
    }
}
