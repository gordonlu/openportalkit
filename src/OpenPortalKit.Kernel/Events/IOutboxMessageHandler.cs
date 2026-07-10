namespace OpenPortalKit.Kernel.Events;

public interface IOutboxMessageHandler
{
    IReadOnlyCollection<string> EventNames { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
