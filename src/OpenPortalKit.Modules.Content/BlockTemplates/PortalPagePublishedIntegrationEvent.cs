using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PortalPagePublishedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string IdempotencyKey,
    Guid SiteId,
    Guid PortalPageId,
    string Slug,
    string Title,
    string Summary,
    DateTimeOffset PublishedAt)
    : IntegrationEvent(
        EventId,
        OccurredAt,
        PublishingEventNames.PortalPagePublished,
        IdempotencyKey);
