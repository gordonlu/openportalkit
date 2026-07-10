namespace OpenPortalKit.Modules.Content.BlockTemplates;

public interface IPageTemplateStore
{
    Task<PageTemplate> SaveAsync(PageTemplate template, CancellationToken cancellationToken = default);

    Task<PageTemplate?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PageTemplate>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PageTemplateVersion>> ListVersionsAsync(
        Guid templateId,
        CancellationToken cancellationToken = default);
}
