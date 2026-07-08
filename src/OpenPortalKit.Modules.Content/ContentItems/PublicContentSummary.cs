namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record PublicContentSummary(
    Guid Id,
    Guid SiteId,
    Guid ContentTypeId,
    string Title,
    string Slug,
    string Summary,
    IReadOnlyList<string> Tags,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt);
