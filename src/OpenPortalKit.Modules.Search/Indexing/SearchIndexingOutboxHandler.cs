using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;

namespace OpenPortalKit.Modules.Search.Indexing;

public sealed class SearchIndexingOutboxHandler : IOutboxMessageHandler
{
    private readonly ISearchIndex _index;
    private readonly IOutboxSearchDocumentResolver _resolver;

    public SearchIndexingOutboxHandler(ISearchIndex index, IOutboxSearchDocumentResolver resolver)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string EventName => PublishingEventNames.ContentPublished;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var document = await _resolver.ResolveAsync(message, cancellationToken);

        if (document is not null)
        {
            await _index.UpsertAsync(document, cancellationToken);
        }
    }
}
