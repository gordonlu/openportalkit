namespace OpenPortalKit.Kernel.Events;

public sealed class OutboxProcessor
{
    private readonly IOutboxMessageStore _messageStore;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IReadOnlyDictionary<string, IOutboxMessageHandler> _handlers;
    private readonly RetryPolicy _retryPolicy;
    private readonly Func<DateTimeOffset> _clock;

    public OutboxProcessor(
        IOutboxMessageStore messageStore,
        IIdempotencyStore idempotencyStore,
        IEnumerable<IOutboxMessageHandler> handlers,
        RetryPolicy? retryPolicy = null,
        Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        _messageStore = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _retryPolicy = retryPolicy ?? RetryPolicy.Default;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
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

        var messages = await _messageStore.ClaimPendingAsync(
            batchSize,
            _retryPolicy.MaxAttemptCount,
            _clock().Add(_retryPolicy.LeaseDuration),
            cancellationToken);

        foreach (var message in messages)
        {
            if (await _idempotencyStore.IsProcessedAsync(message.IdempotencyKey, cancellationToken))
            {
                await _messageStore.MarkProcessedAsync(message.Id, _clock(), cancellationToken);
                skipped++;
                continue;
            }

            if (!_handlers.TryGetValue(message.EventName, out var handler))
            {
                await _messageStore.MarkFailedAsync(
                    message.Id,
                    $"No outbox handler is registered for event '{message.EventName}'.",
                    _clock(),
                    cancellationToken);
                failed++;
                continue;
            }

            try
            {
                await handler.HandleAsync(message, cancellationToken);
                var processedAt = _clock();
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
                    _clock(),
                    cancellationToken);
                failed++;
            }
        }

        return new OutboxProcessingResult(processed, failed, skipped);
    }
}
