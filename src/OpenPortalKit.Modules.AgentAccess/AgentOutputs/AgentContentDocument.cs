namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentContentDocument(
    string Id,
    string ContentType,
    string Title,
    string Slug,
    string Summary,
    string Body,
    Uri CanonicalUrl,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    string? Author,
    string? Source,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> KeyFacts,
    IReadOnlyList<AgentLink> RelatedLinks,
    IReadOnlyList<AgentLink> DataSources,
    AgentVisibilityPolicy VisibilityPolicy,
    string UsagePolicy);
