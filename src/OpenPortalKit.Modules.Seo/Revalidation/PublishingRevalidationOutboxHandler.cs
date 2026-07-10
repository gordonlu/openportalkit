using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;

namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed class PublishingRevalidationOutboxHandler : IOutboxMessageHandler
{
    private readonly PublishingRevalidationPlanner _planner;
    private readonly IPublicOutputRevalidationExecutor _executor;

    public PublishingRevalidationOutboxHandler(
        PublishingRevalidationPlanner planner,
        IPublicOutputRevalidationExecutor executor)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public IReadOnlyCollection<string> EventNames { get; } = new[]
    {
        PublishingEventNames.ContentPublished,
        PublishingEventNames.ContentUpdated,
        PublishingEventNames.ContentArchived,
        PublishingEventNames.PortalPagePublished
    };

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var plan = _planner.CreatePlan(message);
        await _executor.ExecuteAsync(plan, cancellationToken);
    }
}
