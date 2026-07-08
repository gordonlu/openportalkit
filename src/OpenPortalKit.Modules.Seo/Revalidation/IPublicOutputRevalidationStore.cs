namespace OpenPortalKit.Modules.Seo.Revalidation;

public interface IPublicOutputRevalidationStore
{
    Task AddAsync(PublicOutputRevalidationResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicOutputRevalidationResult>> ListAsync(CancellationToken cancellationToken = default);
    Task<PublicOutputRevalidationResult?> FindByIdempotencyKeyAsync(string sourceIdempotencyKey, CancellationToken cancellationToken = default);
}
