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
        var count = 0;

        foreach (var source in _sources)
        {
            var documents = await source.GetDocumentsAsync(cancellationToken);

            foreach (var document in documents)
            {
                await _index.UpsertAsync(document, cancellationToken);
                count++;
            }
        }

        return new SearchReindexResult(count, startedAt, _clock());
    }
}
