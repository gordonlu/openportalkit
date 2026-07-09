namespace OpenPortalKit.Modules.Seo.Revalidation;

public interface IPublicOutputRegenerator
{
    Task<IReadOnlyList<string>> RegenerateAsync(
        PublicOutputRevalidationPlan plan,
        CancellationToken cancellationToken = default);
}
