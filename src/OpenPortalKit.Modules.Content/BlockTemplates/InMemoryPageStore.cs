namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class InMemoryPageStore : IPageStore
{
    private readonly object _gate = new();
    private readonly List<PortalPage> _pages = new();

    public Task<PortalPage> UpsertAsync(PortalPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var index = _pages.FindIndex(candidate => candidate.Id == page.Id);
            if (index >= 0)
            {
                _pages[index] = page;
            }
            else
            {
                _pages.Add(page);
            }
        }

        return Task.FromResult(page);
    }

    public Task<PortalPage?> FindBySlugAsync(Guid siteId, string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_pages.FirstOrDefault(page => page.SiteId == siteId &&
                string.Equals(page.Slug, slug, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<IReadOnlyList<PortalPage>> ListAsync(Guid? siteId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PortalPage>>(_pages
                .Where(page => siteId is null || page.SiteId == siteId)
                .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }
    }
}
