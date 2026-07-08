using OpenPortalKit.Kernel.Entities;

namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record ContentItem(
    Guid Id,
    Guid SiteId,
    Guid ContentTypeId,
    string Title,
    string Slug,
    string Summary,
    string Body,
    Guid? CoverAssetId,
    ContentPublicationStatus Status,
    Guid? CategoryId,
    IReadOnlyList<string> Tags,
    Guid? AuthorId,
    string? Source,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? ExpiresAt,
    Guid CreatedBy,
    Guid UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
