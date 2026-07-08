namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed class InMemoryContentItemStore : IContentItemStore
{
    private readonly object _gate = new();
    private readonly List<ContentItem> _items = new();

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
}
