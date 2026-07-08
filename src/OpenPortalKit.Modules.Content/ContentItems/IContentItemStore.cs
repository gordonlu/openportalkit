namespace OpenPortalKit.Modules.Content.ContentItems;

public interface IContentItemStore
{
    Task<ContentItem> AddAsync(ContentItem item, CancellationToken cancellationToken = default);

    Task<ContentItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ContentItem?> FindBySlugAsync(
        Guid siteId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItem>> ListAsync(
        ContentListQuery query,
        CancellationToken cancellationToken = default);
}
