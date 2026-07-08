using System.Text.Json;

namespace OpenPortalKit.Kernel.Events;

public static class OutboxMessageFactory
{
    public static OutboxMessage FromIntegrationEvent<TEvent>(
        TEvent integrationEvent,
        JsonSerializerOptions? serializerOptions = null)
        where TEvent : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return new OutboxMessage(
            integrationEvent.EventId,
            integrationEvent.EventName,
            JsonSerializer.Serialize(integrationEvent, serializerOptions),
            integrationEvent.IdempotencyKey,
            integrationEvent.OccurredAt,
            ProcessedAt: null,
            AttemptCount: 0,
            LastError: null);
    }
}
