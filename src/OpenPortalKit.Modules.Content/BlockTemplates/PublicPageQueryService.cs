using OpenPortalKit.Modules.Content.ContentItems;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PublicPageQueryService
{
    private readonly IPageStore _store;

    public PublicPageQueryService(IPageStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<PortalPage?> FindPublishedBySlugAsync(
        Guid siteId,
        string slug,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var page = await _store.FindBySlugAsync(siteId, SlugGenerator.Generate(slug), cancellationToken);
        var now = asOf ?? DateTimeOffset.UtcNow;
        return page is not null && page.Status == PortalPageStatus.Published &&
            page.PublishedAt is not null && page.PublishedAt <= now
            ? page
            : null;
    }

    public async Task<IReadOnlyList<PortalPage>> ListPublishedAsync(
        Guid siteId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var now = asOf ?? DateTimeOffset.UtcNow;
        return await _store.ListPublishedAsync(siteId, now, cancellationToken);
    }

    public Task<IReadOnlyList<PortalPage>> ListPublishedPageAsync(
        Guid siteId,
        int skip,
        int take,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default) =>
        _store.ListPublishedPageAsync(siteId, asOf ?? DateTimeOffset.UtcNow, skip, take, cancellationToken);
}
