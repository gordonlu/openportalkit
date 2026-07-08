namespace OpenPortalKit.Modules.Search.Indexing;

public sealed class InMemorySearchIndex : ISearchIndex
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, SearchDocument> _documents = new(StringComparer.Ordinal);

    public Task UpsertAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _documents[document.Id] = document;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _documents.Remove(documentId);
        }

        return Task.CompletedTask;
    }

    public Task<SearchDocument?> FindByIdAsync(string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _documents.TryGetValue(documentId, out var document);
            return Task.FromResult(document);
        }
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Term);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        cancellationToken.ThrowIfCancellationRequested();

        var asOf = query.AsOf ?? DateTimeOffset.UtcNow;
        var targetTypes = new HashSet<string>(query.TargetTypes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var tags = new HashSet<string>(query.Tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>(_documents.Values
                .Where(document => IsVisible(document, query, asOf))
                .Where(document => targetTypes.Count == 0 || targetTypes.Contains(document.TargetType))
                .Where(document => tags.Count == 0 || document.Tags.Any(tags.Contains))
                .Select(document => Score(document, query.Term))
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenByDescending(result => result.Document.PublishedAt ?? result.Document.UpdatedAt)
                .ThenBy(result => result.Document.Title, StringComparer.OrdinalIgnoreCase)
                .Take(query.Limit)
                .ToArray());
        }
    }

    private static bool IsVisible(SearchDocument document, SearchQuery query, DateTimeOffset asOf)
    {
        if (document.Visibility == SearchVisibility.Archived)
        {
            return query.IncludeArchived;
        }

        if (document.Visibility == SearchVisibility.NonPublic && !query.IncludeNonPublic)
        {
            return false;
        }

        if (document.Visibility == SearchVisibility.Public &&
            document.PublishedAt is not null &&
            document.PublishedAt > asOf)
        {
            return false;
        }

        return true;
    }

    private static SearchResult Score(SearchDocument document, string term)
    {
        var score = 0;
        var fields = new List<string>();

        AddScore(document.Title, "title", 8);
        AddScore(document.Summary, "summary", 4);
        AddScore(document.BodyText, "body", 2);
        AddScore(document.Category, "category", 1);

        if (document.Tags.Any(tag => Contains(tag, term)))
        {
            score += 2;
            fields.Add("tags");
        }

        return new SearchResult(document, score, fields);

        void AddScore(string? value, string field, int weight)
        {
            if (Contains(value, term))
            {
                score += weight;
                fields.Add(field);
            }
        }
    }

    private static bool Contains(string? value, string term)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
