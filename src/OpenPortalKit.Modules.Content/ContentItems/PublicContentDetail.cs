namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record PublicContentDetail(
    Guid Id,
    Guid SiteId,
    Guid ContentTypeId,
    string Title,
    string Slug,
    string Summary,
    string Body,
    IReadOnlyList<string> Tags,
    string? Source,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt);
