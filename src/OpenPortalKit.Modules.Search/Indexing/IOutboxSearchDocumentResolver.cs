using OpenPortalKit.Kernel.Events;

namespace OpenPortalKit.Modules.Search.Indexing;

public interface IOutboxSearchDocumentResolver
{
    Task<SearchDocument?> ResolveAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
