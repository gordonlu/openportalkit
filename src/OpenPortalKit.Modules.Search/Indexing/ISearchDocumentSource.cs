namespace OpenPortalKit.Modules.Search.Indexing;

public interface ISearchDocumentSource
{
    Task<IReadOnlyList<SearchDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default);
}
