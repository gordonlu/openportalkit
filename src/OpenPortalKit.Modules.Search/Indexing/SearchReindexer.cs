namespace OpenPortalKit.Modules.Search.Indexing;

public sealed class SearchReindexer
{
    private readonly ISearchIndex _index;
    private readonly IEnumerable<ISearchDocumentSource> _sources;
    private readonly Func<DateTimeOffset> _clock;

    public SearchReindexer(
        ISearchIndex index,
        IEnumerable<ISearchDocumentSource> sources,
        Func<DateTimeOffset>? clock = null)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<SearchReindexResult> ReindexAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = _clock();
        var replacement = new Dictionary<string, SearchDocument>(StringComparer.Ordinal);

        foreach (var source in _sources)
        {
            var documents = await source.GetDocumentsAsync(cancellationToken);

            foreach (var document in documents)
            {
                ArgumentNullException.ThrowIfNull(document);
                ArgumentException.ThrowIfNullOrWhiteSpace(document.Id);
                if (!replacement.TryAdd(document.Id, document))
                {
                    throw new InvalidOperationException(
                        $"Search rebuild produced duplicate document ID '{document.Id}'.");
                }
            }
        }

        await _index.ReplaceAllAsync(replacement.Values.ToArray(), cancellationToken);
        return new SearchReindexResult(replacement.Count, startedAt, _clock());
    }
}
