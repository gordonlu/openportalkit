using System.Text.Json;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public static class AgentOpenApiGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Generate(AgentSiteProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var document = new Dictionary<string, object?>
        {
            ["openapi"] = "3.1.0",
            ["info"] = new Dictionary<string, object?>
            {
                ["title"] = profile.SiteName + " Public API",
                ["version"] = "1.0.0",
                ["description"] = "Read-only public endpoints for OpenPortalKit content, datasets, search, and AgentSEO outputs."
            },
            ["servers"] = new[]
            {
                new Dictionary<string, object?> { ["url"] = profile.BaseUrl.ToString().TrimEnd('/') }
            },
            ["paths"] = BuildPaths()
        };

        return JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
    }

    private static Dictionary<string, object?> BuildPaths()
    {
        return new Dictionary<string, object?>
        {
            ["/api/public"] = GetPath("Public API discovery document."),
            ["/api/public/content"] = GetPath("Published public content summaries."),
            ["/api/public/content/{slug}.json"] = GetPath("Machine-readable JSON snapshot for one public content item."),
            ["/content/{slug}.md"] = GetPath("Markdown snapshot for one public content item."),
            ["/api/public/datasets"] = GetPath("Public dataset summaries."),
            ["/api/public/datasets/{code}"] = GetPath("Public dataset detail with traceability."),
            ["/api/public/datasets/{code}/records"] = GetPath("Public dataset records."),
            ["/api/public/datasets/{code}/schema"] = GetPath("Public dataset schema."),
            ["/api/public/search"] = GetPath("Public full-text search."),
            ["/sitemap.xml"] = GetPath("XML sitemap."),
            ["/rss.xml"] = GetPath("RSS feed."),
            ["/robots.txt"] = GetPath("Crawler and AI bot policy."),
            ["/llms.txt"] = GetPath("Concise LLM discovery file."),
            ["/llms-full.txt"] = GetPath("Full LLM discovery file with content excerpts."),
            ["/.well-known/agent.json"] = GetPath("Agent manifest for machine-readable resource discovery."),
            ["/api/openapi.json"] = GetPath("OpenAPI description for public read endpoints.")
        };
    }

    private static Dictionary<string, object?> GetPath(string description)
    {
        return new Dictionary<string, object?>
        {
            ["get"] = new Dictionary<string, object?>
            {
                ["summary"] = description,
                ["responses"] = new Dictionary<string, object?>
                {
                    ["200"] = new Dictionary<string, object?>
                    {
                        ["description"] = "OK"
                    }
                }
            }
        };
    }
}
