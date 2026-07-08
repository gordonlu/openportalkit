namespace OpenPortalKit.Modules.Seo.Revalidation;

public interface IPublicOutputRevalidationExecutor
{
    Task<PublicOutputRevalidationResult> ExecuteAsync(
        PublicOutputRevalidationPlan plan,
        CancellationToken cancellationToken = default);
}
