namespace OpenPortalKit.Kernel.Events;

public interface IOutboxMessageHandler
{
    string EventName { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
