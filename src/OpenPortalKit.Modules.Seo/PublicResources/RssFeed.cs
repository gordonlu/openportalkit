namespace OpenPortalKit.Modules.Seo.PublicResources;

public sealed record RssFeed(
    string Title,
    string Description,
    Uri Link,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RssFeedItem> Items);
