namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed class InMemoryContentItemStore : IContentItemStore
{
    private readonly object _gate = new();
    private readonly List<ContentItem> _items = new();
    private readonly Dictionary<Guid, List<ContentItemRevision>> _versions = new();

    public Task<ContentItem> AddAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var index = _items.FindIndex(candidate => candidate.Id == item.Id);
            if (index >= 0)
            {
                _items[index] = item;
            }
            else
            {
                _items.Add(item);
            }

            if (!_versions.TryGetValue(item.Id, out var versions))
            {
                versions = new List<ContentItemRevision>();
                _versions[item.Id] = versions;
            }
            versions.Add(new ContentItemRevision(
                item.Id, versions.Count + 1, item, item.UpdatedBy, item.UpdatedAt));
        }

        return Task.FromResult(item);
    }

    public Task<ContentItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }
    }

    public Task<ContentItem?> FindBySlugAsync(
        Guid siteId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_items.FirstOrDefault(item =>
                item.SiteId == siteId &&
                string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<IReadOnlyList<ContentItem>> ListAsync(
        ContentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Take);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var items = _items.AsEnumerable();

            if (query.SiteId is { } siteId)
            {
                items = items.Where(item => item.SiteId == siteId);
            }

            if (query.ContentTypeId is { } contentTypeId)
            {
                items = items.Where(item => item.ContentTypeId == contentTypeId);
            }

            if (query.CategoryId is { } categoryId)
            {
                items = items.Where(item => item.CategoryId == categoryId);
            }

            if (!string.IsNullOrWhiteSpace(query.Tag))
            {
                items = items.Where(item => item.Tags.Any(tag =>
                    string.Equals(tag, query.Tag, StringComparison.OrdinalIgnoreCase)));
            }

            var results = items
                .OrderByDescending(item => item.PublishedAt ?? item.UpdatedAt)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToArray();

            return Task.FromResult<IReadOnlyList<ContentItem>>(results);
        }
    }

    public Task<AdminContentListPage> ListAdminAsync(
        AdminContentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        if (query.Take is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Admin page size must be between 1 and 100.");
        if (query.Search?.Trim().Length > 200)
            throw new ArgumentException("Admin content search must be at most 200 characters.", nameof(query));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var items = _items.AsEnumerable();
            if (query.SiteId is { } siteId) items = items.Where(item => item.SiteId == siteId);
            if (query.Status is { } status) items = items.Where(item => item.Status == status);
            if (query.ContentTypeId is { } contentTypeId)
                items = items.Where(item => item.ContentTypeId == contentTypeId);
            if (query.AuthorId is { } authorId) items = items.Where(item => item.AuthorId == authorId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(item =>
                    item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Slug.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Summary.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = items
                .OrderByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return Task.FromResult(new AdminContentListPage(
                ordered.Skip(query.Skip).Take(query.Take).ToArray(),
                ordered.Length));
        }
    }

    public Task<IReadOnlyList<ContentItem>> ListPublishedAsync(
        ContentListQuery query,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Take);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var items = ApplyListFilters(_items, query)
                .Where(item => item.Status == ContentPublicationStatus.Published &&
                    item.PublishedAt is not null && item.PublishedAt <= asOf &&
                    (item.ExpiresAt is null || item.ExpiresAt > asOf))
                .OrderByDescending(item => item.PublishedAt)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToArray();
            return Task.FromResult<IReadOnlyList<ContentItem>>(items);
        }
    }

    private static IEnumerable<ContentItem> ApplyListFilters(
        IEnumerable<ContentItem> items,
        ContentListQuery query)
    {
        if (query.SiteId is { } siteId) items = items.Where(item => item.SiteId == siteId);
        if (query.ContentTypeId is { } contentTypeId)
            items = items.Where(item => item.ContentTypeId == contentTypeId);
        if (query.CategoryId is { } categoryId) items = items.Where(item => item.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(query.Tag))
            items = items.Where(item => item.Tags.Any(tag =>
                string.Equals(tag, query.Tag, StringComparison.OrdinalIgnoreCase)));
        return items;
    }

    public Task<IReadOnlyList<ContentItemRevision>> ListVersionsAsync(
        Guid contentItemId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_versions.TryGetValue(contentItemId, out var versions))
                return Task.FromResult<IReadOnlyList<ContentItemRevision>>(Array.Empty<ContentItemRevision>());
            return Task.FromResult<IReadOnlyList<ContentItemRevision>>(
                versions.OrderByDescending(version => version.Revision).ToArray());
        }
    }
}
