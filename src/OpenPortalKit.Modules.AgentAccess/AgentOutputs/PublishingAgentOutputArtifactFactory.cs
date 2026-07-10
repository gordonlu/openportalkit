using OpenPortalKit.Modules.Seo.Revalidation;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class PublishingAgentOutputArtifactFactory
{
    private readonly IAgentContentDocumentResolver _resolver;
    private readonly Func<DateTimeOffset> _clock;

    public PublishingAgentOutputArtifactFactory(
        IAgentContentDocumentResolver resolver,
        Func<DateTimeOffset>? clock = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<AgentOutputArtifact>> CreateArtifactsAsync(
        PublicOutputRevalidationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.RegenerateSnapshots)
        {
            return Array.Empty<AgentOutputArtifact>();
        }

        var generatedAt = _clock();
        var artifacts = new List<AgentOutputArtifact>();

        foreach (var slug in ExtractContentSlugs(plan))
        {
            var document = await _resolver.FindPublishedBySlugAsync(plan, slug, cancellationToken);
            if (document is null)
            {
                continue;
            }

            artifacts.AddRange(AgentOutputArtifactGenerator.GenerateContentArtifacts(document, generatedAt));
        }

        return artifacts
            .GroupBy(artifact => artifact.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(artifact => artifact.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> ExtractContentSlugs(PublicOutputRevalidationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return (plan.SnapshotRoutes ?? Array.Empty<string>())
            .Select(TryReadSlug)
            .OfType<string>()
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? TryReadSlug(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        var path = route.Split('?', 2)[0].Trim();

        if (path.StartsWith("/content/", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return path["/content/".Length..^".md".Length].Trim('/');
        }

        if (path.StartsWith("/api/public/content/", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return path["/api/public/content/".Length..^".json".Length].Trim('/');
        }

        return null;
    }
}
