using OpenPortalKit.Modules.Seo.Revalidation;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class FallbackAgentContentDocumentResolver : IAgentContentDocumentResolver
{
    private readonly IReadOnlyList<IAgentContentDocumentResolver> _resolvers;

    public FallbackAgentContentDocumentResolver(IEnumerable<IAgentContentDocumentResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);

        _resolvers = resolvers.ToArray();
        if (_resolvers.Count == 0)
        {
            throw new ArgumentException("At least one agent content resolver is required.", nameof(resolvers));
        }
    }

    public async Task<AgentContentDocument?> FindPublishedBySlugAsync(
        PublicOutputRevalidationPlan plan,
        string slug,
        CancellationToken cancellationToken = default)
    {
        foreach (var resolver in _resolvers)
        {
            var document = await resolver.FindPublishedBySlugAsync(plan, slug, cancellationToken);
            if (document is not null)
            {
                return document;
            }
        }

        return null;
    }
}
