using System.Text.Json;
using OpenPortalKit.Modules.Seo.PublicResources;
using OpenPortalKit.Modules.Seo.Revalidation;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class PublishingEventAgentContentDocumentResolver : IAgentContentDocumentResolver
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AgentOutputGenerationOptions _options;

    public PublishingEventAgentContentDocumentResolver(AgentOutputGenerationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<AgentContentDocument?> FindPublishedBySlugAsync(
        PublicOutputRevalidationPlan plan,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        cancellationToken.ThrowIfCancellationRequested();

        if (!plan.RegenerateSnapshots || string.IsNullOrWhiteSpace(plan.SourcePayloadJson))
        {
            return Task.FromResult<AgentContentDocument?>(null);
        }

        var payload = JsonSerializer.Deserialize<PublishedContentEventPayload>(
            plan.SourcePayloadJson,
            SerializerOptions) ?? throw new InvalidOperationException(
                "Publishing event payload could not be read for AgentSEO artifact generation.");

        ValidatePayload(payload);
        if (!string.Equals(payload.Slug, slug, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<AgentContentDocument?>(null);
        }

        var publicBaseUri = _options.GetPublicBaseUri();
        var document = new AgentContentDocument(
            "content:" + payload.ContentItemId.ToString("D"),
            "Article",
            payload.Title,
            payload.Slug,
            payload.Summary,
            payload.Body,
            CanonicalUrlBuilder.Build(publicBaseUri, "/content/" + payload.Slug),
            payload.PublishedAt,
            payload.UpdatedAt,
            _options.AuthorDisplayName,
            payload.Source,
            payload.Tags ?? Array.Empty<string>(),
            new[]
            {
                "This snapshot is generated from the immutable content-published event payload.",
                "Canonical URLs and source attribution are preserved for agent retrieval."
            },
            new[]
            {
                new AgentLink("Public API discovery", new Uri(publicBaseUri, "/api/public"), "Read-only endpoint catalog."),
                new AgentLink("llms.txt", new Uri(publicBaseUri, "/llms.txt"), "Concise LLM discovery file.")
            },
            new[]
            {
                new AgentLink("Sitemap", new Uri(publicBaseUri, "/sitemap.xml"), "Crawlable public URL inventory."),
                new AgentLink("RSS feed", new Uri(publicBaseUri, "/rss.xml"), "Public publishing feed.")
            },
            AgentVisibilityPolicy.Default,
            _options.UsagePolicy);

        return Task.FromResult<AgentContentDocument?>(document);
    }

    private static void ValidatePayload(PublishedContentEventPayload payload)
    {
        if (payload.ContentItemId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.Slug) ||
            string.IsNullOrWhiteSpace(payload.Title) ||
            string.IsNullOrWhiteSpace(payload.Summary) ||
            string.IsNullOrWhiteSpace(payload.Body) ||
            payload.PublishedAt == default ||
            payload.UpdatedAt == default)
        {
            throw new InvalidOperationException(
                "Publishing event payload is missing required public content fields for AgentSEO artifact generation.");
        }
    }

    private sealed record PublishedContentEventPayload(
        Guid ContentItemId,
        string Slug,
        string Title,
        string Summary,
        string Body,
        string? Source,
        IReadOnlyList<string>? Tags,
        DateTimeOffset PublishedAt,
        DateTimeOffset UpdatedAt);
}
