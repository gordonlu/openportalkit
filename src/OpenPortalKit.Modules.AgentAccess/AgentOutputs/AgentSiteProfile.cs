namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentSiteProfile(
    string SiteName,
    string Description,
    Uri BaseUrl,
    IReadOnlyList<AgentSection> Sections,
    IReadOnlyList<AgentLink> ImportantUrls,
    Uri SitemapUrl,
    Uri RssUrl,
    Uri PublicApiUrl,
    Uri OpenApiUrl,
    Uri LlmsTextUrl,
    Uri LlmsFullTextUrl,
    Uri AgentManifestUrl,
    string UsagePolicy,
    string AttributionPolicy);
