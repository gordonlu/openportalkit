namespace OpenPortalKit.Kernel.Events;

public abstract record IntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string EventName,
    string IdempotencyKey);
