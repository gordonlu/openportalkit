namespace OpenPortalKit.Kernel.Events;

public sealed record OutboxMessage(
    Guid Id,
    string EventName,
    string PayloadJson,
    string IdempotencyKey,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ProcessedAt,
    int AttemptCount,
    string? LastError);
