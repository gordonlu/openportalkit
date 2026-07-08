namespace OpenPortalKit.Modules.Seo.PublicResources;

public sealed record RssFeedItem(
    string Title,
    string Description,
    Uri Link,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    string? Guid = null);
