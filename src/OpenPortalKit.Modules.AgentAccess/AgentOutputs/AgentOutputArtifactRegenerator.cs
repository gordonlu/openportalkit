using OpenPortalKit.Modules.Seo.Revalidation;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class AgentOutputArtifactRegenerator : IPublicOutputRegenerator
{
    private readonly IAgentOutputArtifactStore _store;
    private readonly Func<PublicOutputRevalidationPlan, CancellationToken, Task<IReadOnlyList<AgentOutputArtifact>>> _artifactFactory;

    public AgentOutputArtifactRegenerator(
        IAgentOutputArtifactStore store,
        Func<PublicOutputRevalidationPlan, CancellationToken, Task<IReadOnlyList<AgentOutputArtifact>>> artifactFactory)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _artifactFactory = artifactFactory ?? throw new ArgumentNullException(nameof(artifactFactory));
    }

    public async Task<IReadOnlyList<string>> RegenerateAsync(
        PublicOutputRevalidationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var artifacts = await _artifactFactory(plan, cancellationToken);
        var paths = new List<string>();

        foreach (var artifact in artifacts)
        {
            await _store.UpsertAsync(artifact, cancellationToken);
            paths.Add(artifact.Path);
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
