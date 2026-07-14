namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed class PublicContentQueryService
{
    private readonly IContentItemStore _store;

    public PublicContentQueryService(IContentItemStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<IReadOnlyList<PublicContentSummary>> ListPublishedAsync(
        ContentListQuery query,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = asOf ?? DateTimeOffset.UtcNow;
        var items = await _store.ListPublishedAsync(query, now, cancellationToken);

        return items
            .Where(item => IsPubliclyVisible(item, now))
            .Select(ToSummary)
            .ToArray();
    }

    public async Task<IReadOnlyList<PublicContentDetail>> ListPublishedDetailsAsync(
        ContentListQuery query,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = asOf ?? DateTimeOffset.UtcNow;
        var items = await _store.ListPublishedAsync(query, now, cancellationToken);
        return items.Where(item => IsPubliclyVisible(item, now)).Select(ToDetail).ToArray();
    }

    public async Task<PublicContentDetail?> FindPublishedBySlugAsync(
        Guid siteId,
        string slug,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var now = asOf ?? DateTimeOffset.UtcNow;
        var item = await _store.FindBySlugAsync(siteId, SlugGenerator.Generate(slug), cancellationToken);

        if (item is null || !IsPubliclyVisible(item, now))
        {
            return null;
        }

        return ToDetail(item);
    }

    private static bool IsPubliclyVisible(ContentItem item, DateTimeOffset asOf)
    {
        return item.Status == ContentPublicationStatus.Published &&
            item.PublishedAt is not null &&
            item.PublishedAt <= asOf &&
            (item.ExpiresAt is null || item.ExpiresAt > asOf);
    }

    private static PublicContentSummary ToSummary(ContentItem item)
    {
        return new PublicContentSummary(
            item.Id,
            item.SiteId,
            item.ContentTypeId,
            item.Title,
            item.Slug,
            item.Summary,
            item.Tags,
            item.PublishedAt!.Value,
            item.UpdatedAt);
    }

    private static PublicContentDetail ToDetail(ContentItem item)
    {
        return new PublicContentDetail(
            item.Id,
            item.SiteId,
            item.ContentTypeId,
            item.Title,
            item.Slug,
            item.Summary,
            item.Body,
            item.Tags,
            item.Source,
            item.PublishedAt!.Value,
            item.UpdatedAt);
    }
}
