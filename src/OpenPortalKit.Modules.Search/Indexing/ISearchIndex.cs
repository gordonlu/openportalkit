namespace OpenPortalKit.Modules.Search.Indexing;

public interface ISearchIndex
{
    Task UpsertAsync(SearchDocument document, CancellationToken cancellationToken = default);
    Task DeleteAsync(string documentId, CancellationToken cancellationToken = default);
    Task<SearchDocument?> FindByIdAsync(string documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResult>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);
}
