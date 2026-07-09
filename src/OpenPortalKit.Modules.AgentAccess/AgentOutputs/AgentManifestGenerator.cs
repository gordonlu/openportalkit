using System.Text.Json;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public static class AgentManifestGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static AgentManifest Create(
        AgentSiteProfile profile,
        AgentBotPolicy botPolicy,
        Uri publicSearchEndpoint,
        IReadOnlyList<AgentLink> datasetEndpoints)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(botPolicy);
        ArgumentNullException.ThrowIfNull(publicSearchEndpoint);
        ArgumentNullException.ThrowIfNull(datasetEndpoints);

        return new AgentManifest(
            profile.SiteName,
            profile.Description,
            profile.ImportantUrls,
            profile.SitemapUrl,
            profile.RssUrl,
            profile.LlmsTextUrl,
            profile.LlmsFullTextUrl,
            profile.OpenApiUrl,
            publicSearchEndpoint,
            datasetEndpoints,
            botPolicy,
            profile.UsagePolicy,
            profile.AttributionPolicy);
    }

    public static string GenerateJson(
        AgentSiteProfile profile,
        AgentBotPolicy botPolicy,
        Uri publicSearchEndpoint,
        IReadOnlyList<AgentLink> datasetEndpoints)
    {
        return JsonSerializer.Serialize(
            Create(profile, botPolicy, publicSearchEndpoint, datasetEndpoints),
            JsonOptions) + Environment.NewLine;
    }
}
