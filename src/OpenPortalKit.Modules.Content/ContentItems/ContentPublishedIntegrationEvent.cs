using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;

namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record ContentPublishedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string IdempotencyKey,
    Guid SiteId,
    Guid ContentItemId,
    string Slug,
    DateTimeOffset PublishedAt)
    : IntegrationEvent(
        EventId,
        OccurredAt,
        PublishingEventNames.ContentPublished,
        IdempotencyKey);
