namespace OpenPortalKit.Kernel.Events;

public abstract record DomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string EventName);
