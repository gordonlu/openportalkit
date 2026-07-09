namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentManifest(
    string SiteName,
    string Description,
    IReadOnlyList<AgentLink> PublicResources,
    Uri Sitemap,
    Uri Rss,
    Uri LlmsText,
    Uri LlmsFullText,
    Uri OpenApiSpec,
    Uri PublicSearchEndpoint,
    IReadOnlyList<AgentLink> DatasetEndpoints,
    AgentBotPolicy BotPolicy,
    string UsagePolicy,
    string AttributionPolicy);
