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

    Task<IReadOnlyList<ContentItem>> ListPublishedAsync(
        ContentListQuery query,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task<AdminContentListPage> ListAdminAsync(
        AdminContentListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentItemRevision>> ListVersionsAsync(
        Guid contentItemId,
        CancellationToken cancellationToken = default);
}
