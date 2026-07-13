namespace OpenPortalKit.Modules.Content.BlockTemplates;

public interface IPageStore
{
    Task<PortalPage> UpsertAsync(PortalPage page, CancellationToken cancellationToken = default);

    Task<PortalPage?> FindBySlugAsync(Guid siteId, string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalPage>> ListAsync(Guid? siteId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortalPageVersion>> ListVersionsAsync(
        Guid pageId,
        CancellationToken cancellationToken = default);
}
