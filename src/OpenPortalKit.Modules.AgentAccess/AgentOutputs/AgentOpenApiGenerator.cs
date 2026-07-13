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
            ["/api/public/content"] = GetPaginatedPath("Published public content summaries."),
            ["/api/public/content/{slug}.json"] = GetConditionalPath("Machine-readable JSON snapshot for one public content item."),
            ["/content/{slug}.md"] = GetConditionalPath("Markdown snapshot for one public content item."),
            ["/pages/{slug}"] = GetConditionalPath("Server-rendered public page assembled from a versioned template."),
            ["/pages/{slug}.md"] = GetConditionalPath("Markdown snapshot for one public page."),
            ["/api/public/pages/{slug}.json"] = GetConditionalPath("Machine-readable JSON snapshot for one public page."),
            ["/api/public/datasets"] = GetPath("Public dataset summaries."),
            ["/api/public/datasets/{code}"] = GetConditionalPath("Public dataset detail with traceability."),
            ["/api/public/datasets/{code}/records"] = GetPaginatedPath("Public dataset records."),
            ["/api/public/datasets/{code}/records/{recordKey}"] = GetConditionalPath("One traceable public dataset record."),
            ["/api/public/datasets/{code}/schema"] = GetConditionalPath("Public dataset schema."),
            ["/api/public/datasets/{code}/export.csv"] = GetConditionalPath("CSV export of public dataset records."),
            ["/api/public/search"] = GetPaginatedPath("Public full-text search."),
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

    private static Dictionary<string, object?> GetConditionalPath(string description)
    {
        var path = GetPath(description);
        var operation = (Dictionary<string, object?>)path["get"]!;
        operation["responses"] = new Dictionary<string, object?>
        {
            ["200"] = new Dictionary<string, object?> { ["description"] = "OK" },
            ["304"] = new Dictionary<string, object?> { ["description"] = "Not modified" },
            ["404"] = new Dictionary<string, object?> { ["description"] = "Not found" }
        };
        return path;
    }

    private static Dictionary<string, object?> GetPaginatedPath(string description)
    {
        var path = GetPath(description);
        var operation = (Dictionary<string, object?>)path["get"]!;
        operation["parameters"] = new object[]
        {
            QueryInteger("offset", 0, 0, null),
            QueryInteger("limit", 20, 1, 100)
        };
        operation["responses"] = new Dictionary<string, object?>
        {
            ["200"] = new Dictionary<string, object?> { ["description"] = "Paginated response" },
            ["400"] = new Dictionary<string, object?> { ["description"] = "Invalid pagination" }
        };
        return path;
    }

    private static Dictionary<string, object?> QueryInteger(
        string name,
        int defaultValue,
        int minimum,
        int? maximum)
    {
        var schema = new Dictionary<string, object?>
        {
            ["type"] = "integer",
            ["default"] = defaultValue,
            ["minimum"] = minimum
        };
        if (maximum is not null) schema["maximum"] = maximum;
        return new Dictionary<string, object?>
        {
            ["name"] = name,
            ["in"] = "query",
            ["required"] = false,
            ["schema"] = schema
        };
    }
}
