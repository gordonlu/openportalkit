namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class InMemoryPageStore : IPageStore
{
    private readonly object _gate = new();
    private readonly List<PortalPage> _pages = new();
    private readonly List<PortalPageVersion> _versions = new();

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

            if (_versions.All(version => version.PageId != page.Id || version.Revision != page.Revision))
            {
                _versions.Add(new PortalPageVersion(page.Id, page.Revision, page, page.UpdatedBy, page.UpdatedAt));
            }
        }

        return Task.FromResult(page);
    }

    public Task<bool> TryUpdateAsync(
        PortalPage page,
        int expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedRevision);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var index = _pages.FindIndex(candidate => candidate.Id == page.Id);
            if (index < 0 || _pages[index].Revision != expectedRevision) return Task.FromResult(false);
            _pages[index] = page;
            if (_versions.All(version => version.PageId != page.Id || version.Revision != page.Revision))
                _versions.Add(new PortalPageVersion(page.Id, page.Revision, page, page.UpdatedBy, page.UpdatedAt));
            return Task.FromResult(true);
        }
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

    public Task<PortalPage?> FindByIdAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_pages.FirstOrDefault(page => page.Id == pageId));
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

    public Task<IReadOnlyList<PortalPage>> ListPublishedAsync(
        Guid siteId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PortalPage>>(_pages
                .Where(page => page.SiteId == siteId &&
                    page.Status == PortalPageStatus.Published &&
                    page.PublishedAt is not null &&
                    page.PublishedAt <= asOf)
                .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(page => page.Id)
                .ToArray());
        }
    }

    public Task<IReadOnlyList<PortalPage>> ListPublishedPageAsync(
        Guid siteId,
        DateTimeOffset asOf,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PortalPage>>(_pages
                .Where(page => page.SiteId == siteId &&
                    page.Status == PortalPageStatus.Published &&
                    page.PublishedAt is not null && page.PublishedAt <= asOf)
                .OrderByDescending(page => page.PublishedAt)
                .ThenBy(page => page.Id)
                .Skip(skip)
                .Take(take)
                .ToArray());
        }
    }

    public Task<IReadOnlyList<PortalPageVersion>> ListVersionsAsync(
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PortalPageVersion>>(_versions
                .Where(version => version.PageId == pageId)
                .OrderByDescending(version => version.Revision)
                .ToArray());
        }
    }
}
