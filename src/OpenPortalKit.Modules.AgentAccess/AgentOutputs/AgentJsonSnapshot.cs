namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentJsonSnapshot(
    string Id,
    string ContentType,
    string Title,
    string Slug,
    string Summary,
    IReadOnlyList<string> KeyFacts,
    string BodyText,
    string? Source,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    Uri CanonicalUrl,
    IReadOnlyList<AgentLink> Citations,
    IReadOnlyList<AgentLink> RelatedContent,
    AgentVisibilityPolicy AgentVisibilityPolicy,
    string UsagePolicy);
