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
                ["version"] = PublicApiContract.Version,
                ["description"] = "Read-only public endpoints for OpenPortalKit content, datasets, search, and AgentSEO outputs."
            },
            ["servers"] = new[]
            {
                new Dictionary<string, object?> { ["url"] = profile.BaseUrl.ToString().TrimEnd('/') }
            },
            ["paths"] = BuildPaths(),
            ["x-openportalkit-contract-version"] = PublicApiContract.Version
        };

        return JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
    }

    private static Dictionary<string, object?> BuildPaths()
    {
        return new Dictionary<string, object?>
        {
            ["/api/public"] = GetPath("/api/public", "getPublicApiDiscovery", "Public API discovery document."),
            ["/api/public/content"] = GetPaginatedPath("/api/public/content", "listPublicContent", "Published public content summaries.", 20),
            ["/content/{slug}"] = GetConditionalPath("/content/{slug}", "getPublicContentHtml", "Semantic HTML for one public content item."),
            ["/api/public/content/{slug}.json"] = GetConditionalPath("/api/public/content/{slug}.json", "getPublicContentJson", "Machine-readable JSON snapshot for one public content item."),
            ["/content/{slug}.md"] = GetConditionalPath("/content/{slug}.md", "getPublicContentMarkdown", "Markdown snapshot for one public content item."),
            ["/pages/{slug}"] = GetConditionalPath("/pages/{slug}", "getPublicPageHtml", "Server-rendered public page assembled from a versioned template."),
            ["/pages/{slug}.md"] = GetConditionalPath("/pages/{slug}.md", "getPublicPageMarkdown", "Markdown snapshot for one public page."),
            ["/api/public/pages/{slug}.json"] = GetConditionalPath("/api/public/pages/{slug}.json", "getPublicPageJson", "Machine-readable JSON snapshot for one public page."),
            ["/api/public/datasets"] = GetPath("/api/public/datasets", "listPublicDatasets", "Public dataset summaries."),
            ["/api/public/datasets/{code}"] = GetConditionalPath("/api/public/datasets/{code}", "getPublicDataset", "Public dataset detail with traceability."),
            ["/api/public/datasets/{code}/records"] = GetPaginatedPath("/api/public/datasets/{code}/records", "listPublicDatasetRecords", "Public dataset records.", 50),
            ["/api/public/datasets/{code}/records/{recordKey}"] = GetConditionalPath("/api/public/datasets/{code}/records/{recordKey}", "getPublicDatasetRecord", "One traceable public dataset record."),
            ["/api/public/datasets/{code}/schema"] = GetConditionalPath("/api/public/datasets/{code}/schema", "getPublicDatasetSchema", "Public dataset schema."),
            ["/api/public/datasets/{code}/export.csv"] = GetConditionalPath("/api/public/datasets/{code}/export.csv", "exportPublicDatasetCsv", "CSV export of public dataset records."),
            ["/api/public/search"] = GetSearchPath(),
            ["/sitemap.xml"] = GetPath("/sitemap.xml", "getSitemap", "XML sitemap."),
            ["/rss.xml"] = GetPath("/rss.xml", "getRssFeed", "RSS feed."),
            ["/robots.txt"] = GetPath("/robots.txt", "getRobotsPolicy", "Crawler and AI bot policy."),
            ["/llms.txt"] = GetPath("/llms.txt", "getLlmsText", "Concise LLM discovery file."),
            ["/llms-full.txt"] = GetPath("/llms-full.txt", "getFullLlmsText", "Full LLM discovery file with content excerpts."),
            ["/.well-known/agent.json"] = GetPath("/.well-known/agent.json", "getAgentManifest", "Agent manifest for machine-readable resource discovery."),
            ["/api/openapi.json"] = GetPath("/api/openapi.json", "getPublicOpenApi", "OpenAPI description for public read endpoints.")
        };
    }

    private static Dictionary<string, object?> GetPath(string route, string operationId, string description)
    {
        var operation = new Dictionary<string, object?>
        {
            ["operationId"] = operationId,
            ["summary"] = description,
            ["responses"] = new Dictionary<string, object?>
            {
                ["200"] = new Dictionary<string, object?> { ["description"] = "OK" }
            }
        };
        var pathParameters = ExtractPathParameters(route).Select(PathString).Cast<object>().ToArray();
        if (pathParameters.Length > 0) operation["parameters"] = pathParameters;

        return new Dictionary<string, object?>
        {
            ["get"] = operation
        };
    }

    private static Dictionary<string, object?> GetConditionalPath(string route, string operationId, string description)
    {
        var path = GetPath(route, operationId, description);
        var operation = (Dictionary<string, object?>)path["get"]!;
        operation["responses"] = new Dictionary<string, object?>
        {
            ["200"] = new Dictionary<string, object?> { ["description"] = "OK" },
            ["304"] = new Dictionary<string, object?> { ["description"] = "Not modified" },
            ["404"] = new Dictionary<string, object?> { ["description"] = "Not found" }
        };
        return path;
    }

    private static Dictionary<string, object?> GetPaginatedPath(
        string route,
        string operationId,
        string description,
        int defaultLimit)
    {
        var path = GetPath(route, operationId, description);
        var operation = (Dictionary<string, object?>)path["get"]!;
        var parameters = operation.TryGetValue("parameters", out var existing)
            ? ((object[])existing!).ToList()
            : new List<object>();
        parameters.Add(QueryInteger("offset", 0, 0, null));
        parameters.Add(QueryInteger("limit", defaultLimit, 1, 100));
        operation["parameters"] = parameters.ToArray();
        operation["responses"] = new Dictionary<string, object?>
        {
            ["200"] = new Dictionary<string, object?> { ["description"] = "Paginated response" },
            ["400"] = new Dictionary<string, object?> { ["description"] = "Invalid pagination" }
        };
        return path;
    }

    private static Dictionary<string, object?> GetSearchPath()
    {
        var path = GetPaginatedPath("/api/public/search", "searchPublicResources", "Public full-text search.", 20);
        var operation = (Dictionary<string, object?>)path["get"]!;
        var parameters = ((object[])operation["parameters"]!).ToList();
        parameters.Insert(0, new Dictionary<string, object?>
        {
            ["name"] = "q",
            ["in"] = "query",
            ["required"] = true,
            ["schema"] = new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["minLength"] = 1,
                ["maxLength"] = 200
            }
        });
        operation["parameters"] = parameters.ToArray();
        return path;
    }

    private static IEnumerable<string> ExtractPathParameters(string route)
    {
        var start = 0;
        while ((start = route.IndexOf('{', start)) >= 0)
        {
            var end = route.IndexOf('}', start + 1);
            if (end < 0) yield break;
            yield return route[(start + 1)..end];
            start = end + 1;
        }
    }

    private static Dictionary<string, object?> PathString(string name) => new()
    {
        ["name"] = name,
        ["in"] = "path",
        ["required"] = true,
        ["schema"] = new Dictionary<string, object?>
        {
            ["type"] = "string",
            ["minLength"] = 1
        }
    };

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
