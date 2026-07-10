using OpenPortalKit.Modules.AgentAccess.AgentOutputs;
using OpenPortalKit.Modules.Content.ContentItems;
using OpenPortalKit.Modules.Seo.PublicResources;
using OpenPortalKit.Modules.Seo.Revalidation;

namespace OpenPortalKit.AdminHost.AgentAccess;

public sealed class ContentStoreAgentContentDocumentResolver : IAgentContentDocumentResolver
{
    private const int SearchTake = 500;

    private readonly IContentItemStore _contentStore;
    private readonly AgentOutputGenerationOptions _options;

    public ContentStoreAgentContentDocumentResolver(
        IContentItemStore contentStore,
        AgentOutputGenerationOptions options)
    {
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AgentContentDocument?> FindPublishedBySlugAsync(
        PublicOutputRevalidationPlan plan,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedSlug = SlugGenerator.Generate(slug);
        var item = await FindContentItemAsync(normalizedSlug, cancellationToken);
        if (item is null || !IsPubliclyVisible(item, DateTimeOffset.UtcNow))
        {
            return null;
        }

        return BuildDocument(item, _options.GetPublicBaseUri());
    }

    private async Task<ContentItem?> FindContentItemAsync(
        string normalizedSlug,
        CancellationToken cancellationToken)
    {
        if (_options.SiteId is Guid siteId)
        {
            return await _contentStore.FindBySlugAsync(siteId, normalizedSlug, cancellationToken);
        }

        var items = await _contentStore.ListAsync(
            new ContentListQuery(Take: SearchTake),
            cancellationToken);

        return items.FirstOrDefault(item =>
            string.Equals(item.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase));
    }

    private AgentContentDocument BuildDocument(ContentItem item, Uri publicBaseUri)
    {
        return new AgentContentDocument(
            "content:" + item.Id.ToString("D"),
            "Article",
            item.Title,
            item.Slug,
            item.Summary,
            item.Body,
            CanonicalUrlBuilder.Build(publicBaseUri, "/content/" + item.Slug),
            item.PublishedAt!.Value,
            item.UpdatedAt,
            _options.AuthorDisplayName,
            item.Source,
            item.Tags,
            new[]
            {
                "Public outputs include HTML, XML feeds, Markdown snapshots, JSON snapshots, and API descriptions.",
                "Machine-readable resources must preserve canonical URLs and source attribution.",
                "Agent-facing access is governed by robots.txt and the agent manifest."
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
    }

    private static bool IsPubliclyVisible(ContentItem item, DateTimeOffset asOf)
    {
        return item.Status == ContentPublicationStatus.Published &&
            item.PublishedAt is not null &&
            item.PublishedAt <= asOf &&
            (item.ExpiresAt is null || item.ExpiresAt > asOf);
    }
}
