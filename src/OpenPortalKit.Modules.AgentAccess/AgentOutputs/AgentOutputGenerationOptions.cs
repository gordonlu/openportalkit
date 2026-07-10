namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class AgentOutputGenerationOptions
{
    public const string SectionName = "OpenPortalKit:AgentAccess:OutputGeneration";

    public string PublicBaseUrl { get; set; } = "http://localhost:5051";

    public Guid? SiteId { get; set; }

    public string AuthorDisplayName { get; set; } = "OpenPortalKit Editorial";

    public string UsagePolicy { get; set; } =
        "This public snapshot may be used for search indexing, citation, and retrieval-augmented generation when the canonical URL and source attribution are preserved.";

    public Uri GetPublicBaseUri()
    {
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Agent output generation requires an absolute public base URL.");
        }

        return uri;
    }
}
