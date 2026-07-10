using System.Diagnostics;
using System.Threading.Channels;
using System.Data.Common;
using Npgsql;
using OpenPortalKit.Kernel.Configuration;
using OpenPortalKit.Modules.AgentAccess;
using OpenPortalKit.Modules.AgentAccess.AgentOutputs;
using OpenPortalKit.Modules.Assets;
using OpenPortalKit.Modules.Audit;
using OpenPortalKit.Modules.Content;
using OpenPortalKit.Modules.Content.ContentItems;
using OpenPortalKit.Modules.Dashboard;
using OpenPortalKit.Modules.Dashboard.Analytics;
using OpenPortalKit.Modules.Dashboard.Storage;
using OpenPortalKit.Modules.Data;
using OpenPortalKit.Modules.Data.Datasets;
using OpenPortalKit.Modules.Identity;
using OpenPortalKit.Modules.Jobs;
using OpenPortalKit.Modules.Search;
using OpenPortalKit.Modules.Search.Indexing;
using OpenPortalKit.Modules.Seo;
using OpenPortalKit.Modules.Seo.PublicResources;
using OpenPortalKit.Modules.Seo.Redirects;
using OpenPortalKit.Modules.Workflow;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

builder.Services.AddHealthChecks();
builder.Services.Configure<OpenPortalKitStorageOptions>(
    builder.Configuration.GetSection(OpenPortalKitStorageOptions.SectionName));
builder.Services.Configure<AnalyticsPrivacyOptions>(
    builder.Configuration.GetSection(AnalyticsPrivacyOptions.SectionName));
builder.Services.Configure<AgentBotPolicyOptions>(
    builder.Configuration.GetSection(AgentBotPolicyOptions.SectionName));
var dashboardPostgres = builder.Configuration
    .GetSection(DashboardPostgresStorageOptions.SectionName)
    .Get<DashboardPostgresStorageOptions>() ?? new DashboardPostgresStorageOptions();
if (string.IsNullOrWhiteSpace(dashboardPostgres.ConnectionString))
{
    dashboardPostgres.ConnectionString = builder.Configuration.GetConnectionString(
        dashboardPostgres.ConnectionStringName);
}

builder.Services.AddSingleton(dashboardPostgres);
var agentOutputPostgres = builder.Configuration
    .GetSection(AgentOutputPostgresStorageOptions.SectionName)
    .Get<AgentOutputPostgresStorageOptions>() ?? new AgentOutputPostgresStorageOptions();
if (string.IsNullOrWhiteSpace(agentOutputPostgres.ConnectionString))
{
    agentOutputPostgres.ConnectionString = builder.Configuration.GetConnectionString(
        agentOutputPostgres.ConnectionStringName);
}

builder.Services.AddSingleton(agentOutputPostgres);
if (dashboardPostgres.Enabled)
{
    builder.Services.AddSingleton<IDashboardDbConnectionFactory, DashboardPostgresConnectionFactory>();
    builder.Services.AddSingleton<IAnalyticsEventStore, PostgresAnalyticsEventStore>();
}
else
{
    builder.Services.AddSingleton<IAnalyticsEventStore, InMemoryAnalyticsEventStore>();
}
if (agentOutputPostgres.Enabled)
{
    builder.Services.AddSingleton<IAgentOutputDbConnectionFactory, AgentOutputPostgresConnectionFactory>();
    builder.Services.AddSingleton<IAgentOutputArtifactStore, PostgresAgentOutputArtifactStore>();
}
else
{
    builder.Services.AddSingleton<IAgentOutputArtifactStore, InMemoryAgentOutputArtifactStore>();
}

builder.Services.AddSingleton<AnalyticsEventFactory>(provider =>
    new AnalyticsEventFactory(provider.GetRequiredService<IOptions<AnalyticsPrivacyOptions>>().Value));
builder.Services.AddSingleton<ApiAnalyticsCaptureQueue>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ApiAnalyticsCaptureQueue>());
builder.Services.AddSingleton<IContentItemStore>(_ => BuildSeedContentItemStore());

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (!IsPublicOutputRequest(context.Request))
    {
        await next();
        return;
    }

    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    var analyticsEvent = CreateAnalyticsEvent(
        context,
        "api_request",
        new Dictionary<string, string>
        {
            ["method"] = context.Request.Method,
            ["status_code"] = context.Response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["latency_ms"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["public_output"] = "true"
        });
    context.RequestServices
        .GetRequiredService<ApiAnalyticsCaptureQueue>()
        .TryEnqueue(analyticsEvent);
});

var modules = new[]
{
    IdentityModule.Descriptor,
    ContentModule.Descriptor,
    AssetsModule.Descriptor,
    WorkflowModule.Descriptor,
    DataModule.Descriptor,
    SearchModule.Descriptor,
    SeoModule.Descriptor,
    AgentAccessModule.Descriptor,
    DashboardModule.Descriptor,
    AuditModule.Descriptor,
    JobsModule.Descriptor
};

app.MapGet("/", () => Results.Redirect("/health"));
app.MapHealthChecks("/health");

app.MapGet("/api/system/modules", () => modules.Select(module => new
{
    module.Name,
    module.Area,
    module.Description,
    module.OwnsBusinessState,
    module.PublicOutputs
}));

app.MapGet("/api/system/storage", (IConfiguration configuration) =>
{
    var storage = configuration
        .GetSection(OpenPortalKitStorageOptions.SectionName)
        .Get<OpenPortalKitStorageOptions>() ?? new OpenPortalKitStorageOptions();

    return new
    {
        storage.Provider,
        storage.PrimaryConnectionStringName,
        PrimaryConnectionConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString(storage.PrimaryConnectionStringName)),
        storage.CacheConnectionStringName,
        CacheConnectionConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString(storage.CacheConnectionStringName))
    };
});

app.MapGet("/api/public", (HttpRequest request) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);

    return new
    {
        Name = "OpenPortalKit Public API",
        Status = "ready",
        Resources = new[]
        {
            new { Rel = "content", Href = new Uri(siteBaseUrl, "/api/public/content").ToString() },
            new { Rel = "datasets", Href = new Uri(siteBaseUrl, "/api/public/datasets").ToString() },
            new { Rel = "search", Href = new Uri(siteBaseUrl, "/api/public/search").ToString() },
            new { Rel = "sitemap", Href = new Uri(siteBaseUrl, "/sitemap.xml").ToString() },
            new { Rel = "rss", Href = new Uri(siteBaseUrl, "/rss.xml").ToString() },
            new { Rel = "llms", Href = new Uri(siteBaseUrl, "/llms.txt").ToString() },
            new { Rel = "llms-full", Href = new Uri(siteBaseUrl, "/llms-full.txt").ToString() },
            new { Rel = "agent-manifest", Href = new Uri(siteBaseUrl, "/.well-known/agent.json").ToString() },
            new { Rel = "openapi", Href = new Uri(siteBaseUrl, "/api/openapi.json").ToString() }
        }
    };
});

app.MapGet("/analytics/client.js", () => Results.Text(BuildAnalyticsClientScript(), "text/javascript; charset=utf-8"));

app.MapPost("/analytics/events", async (
    AnalyticsEventCaptureRequest request,
    HttpContext context,
    IOptions<AnalyticsPrivacyOptions> options,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.EventType) ||
        string.IsNullOrWhiteSpace(request.Path))
    {
        return Results.BadRequest(new { Error = "eventType and path are required." });
    }

    var metadata = request.Metadata?.ToDictionary() ?? new Dictionary<string, string>();
    metadata.TryAdd("client_type", "browser");

    var analyticsEvent = await StoreAnalyticsEventAsync(
        context,
        request.EventType,
        metadata,
        request.SiteId,
        request.Path,
        request.SessionId,
        request.OccurredAt,
        request.Referrer,
        request.UserAgent,
        request.IpAddress,
        cancellationToken);
    await context.RequestServices
        .GetRequiredService<IAnalyticsEventStore>()
        .DeleteOlderThanAsync(
            DateTimeOffset.UtcNow.AddDays(-Math.Max(1, options.Value.RetentionDays)),
            cancellationToken);

    return Results.Accepted("/admin/analytics/events", new
    {
        analyticsEvent.Id,
        analyticsEvent.SiteId,
        analyticsEvent.EventType,
        analyticsEvent.Path,
        analyticsEvent.OccurredAt,
        analyticsEvent.IsBot
    });
});

app.MapGet("/robots.txt", (HttpRequest request, IOptions<AgentBotPolicyOptions> botPolicyOptions) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var policy = new RobotsPolicy(
        new Uri(siteBaseUrl, "/sitemap.xml"),
        BuildRobotsDirectives(botPolicyOptions.Value.ToPolicy()));

    return Results.Text(RobotsTxtGenerator.Generate(policy), "text/plain");
});

app.MapGet("/sitemap.xml", async (HttpRequest request, IContentItemStore contentStore) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var resources = await GetPublicResourceDescriptorsAsync(siteBaseUrl, contentStore);
    var entries = resources
        .Select(resource => new SitemapEntry(
            CanonicalUrlBuilder.Build(siteBaseUrl, resource.Path),
            resource.UpdatedAt,
            SitemapChangeFrequency.Weekly,
            0.7m));

    return Results.Text(SitemapXmlGenerator.Generate(entries), "application/xml");
});

app.MapGet("/rss.xml", async (HttpRequest request, IContentItemStore contentStore) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var resources = await GetPublicResourceDescriptorsAsync(siteBaseUrl, contentStore);
    var feed = new RssFeed(
        "OpenPortalKit Public Feed",
        "Latest public OpenPortalKit content.",
        siteBaseUrl,
        resources.Max(resource => resource.UpdatedAt),
        resources.Select(resource => new RssFeedItem(
            resource.Title,
            resource.Description,
            CanonicalUrlBuilder.Build(siteBaseUrl, resource.Path),
            resource.PublishedAt,
            resource.UpdatedAt,
            resource.Path)).ToArray());

    return Results.Text(RssXmlGenerator.Generate(feed), "application/rss+xml");
});

app.MapGet("/llms.txt", async (HttpRequest request, IContentItemStore contentStore) =>
{
    var profile = BuildAgentSiteProfile(request);
    var documents = await GetAgentContentDocumentsAsync(profile.BaseUrl, contentStore);

    return Results.Text(LlmsTextGenerator.Generate(profile, documents, includeFullContent: false), "text/plain; charset=utf-8");
});

app.MapGet("/llms-full.txt", async (HttpRequest request, IContentItemStore contentStore) =>
{
    var profile = BuildAgentSiteProfile(request);
    var documents = await GetAgentContentDocumentsAsync(profile.BaseUrl, contentStore);

    return Results.Text(LlmsTextGenerator.Generate(profile, documents, includeFullContent: true), "text/plain; charset=utf-8");
});

app.MapGet("/.well-known/agent.json", (
    HttpRequest request,
    IOptions<AgentBotPolicyOptions> botPolicyOptions) =>
{
    var profile = BuildAgentSiteProfile(request);
    var manifest = AgentManifestGenerator.GenerateJson(
        profile,
        botPolicyOptions.Value.ToPolicy(),
        new Uri(profile.BaseUrl, "/api/public/search"),
        GetSampleDataSets()
            .Select(dataSet => new AgentLink(dataSet.Name, new Uri(profile.BaseUrl, "/api/public/datasets/" + dataSet.Code), dataSet.Description))
            .ToArray());

    return Results.Text(manifest, "application/json; charset=utf-8");
});

app.MapGet("/api/openapi.json", (HttpRequest request) =>
{
    return Results.Text(AgentOpenApiGenerator.Generate(BuildAgentSiteProfile(request)), "application/json; charset=utf-8");
});

app.MapGet("/api/public/content", async (HttpRequest request, IContentItemStore contentStore) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var documents = await GetAgentContentDocumentsAsync(siteBaseUrl, contentStore);

    return documents
        .Select(document => new
        {
            document.Id,
            document.ContentType,
            document.Title,
            document.Slug,
            document.Summary,
            document.CanonicalUrl,
            MarkdownSnapshot = new Uri(siteBaseUrl, "/content/" + document.Slug + ".md"),
            JsonSnapshot = new Uri(siteBaseUrl, "/api/public/content/" + document.Slug + ".json"),
            document.PublishedAt,
            document.UpdatedAt,
            document.VisibilityPolicy
        });
});

app.MapGet("/content/{slug}.md", async (string slug, HttpRequest request, IContentItemStore contentStore) =>
{
    var document = await FindAgentContentDocumentAsync(GetSiteBaseUrl(request), contentStore, slug);

    return document is null
        ? Results.NotFound()
        : Results.Text(AgentSnapshotGenerator.GenerateMarkdown(document), "text/markdown; charset=utf-8");
});

app.MapGet("/api/public/content/{slug}.json", async (string slug, HttpRequest request, IContentItemStore contentStore) =>
{
    var document = await FindAgentContentDocumentAsync(GetSiteBaseUrl(request), contentStore, slug);

    return document is null
        ? Results.NotFound()
        : Results.Text(AgentSnapshotGenerator.GenerateJson(document), "application/json; charset=utf-8");
});

app.MapGet("/api/public/content/sample/metadata", (HttpRequest request) =>
{
    var resource = GetSeedPublicResourceDescriptors()[0];
    return SeoPageMetadataBuilder.Build(resource, GetSiteBaseUrl(request), "OpenPortalKit");
});

app.MapGet("/api/public/redirects/resolve", async (string path) =>
{
    var resolver = new RedirectResolver(GetSampleRedirectRuleStore());
    var resolution = await resolver.ResolveAsync(path);

    return resolution is null ? Results.NotFound() : Results.Ok(resolution);
});

app.MapGet("/api/public/datasets", () => GetSampleDataSets().Select(dataSet => new PublicDataSetSummary(
    dataSet.Code,
    dataSet.Name,
    dataSet.Description)));

app.MapGet("/api/public/datasets/{code}", async (string code) =>
{
    var sample = await BuildSampleDataContextAsync();
    var query = new PublicDataSetQueryService(sample.DataSetStore, sample.RecordStore);
    var detail = await query.FindByCodeAsync(sample.SiteId, code);

    return detail is null ? Results.NotFound() : Results.Ok(detail);
});

app.MapGet("/api/public/datasets/{code}/records", async (string code) =>
{
    var sample = await BuildSampleDataContextAsync();
    var query = new PublicDataSetQueryService(sample.DataSetStore, sample.RecordStore);
    var detail = await query.FindByCodeAsync(sample.SiteId, code);

    return detail is null ? Results.NotFound() : Results.Ok(detail.Records);
});

app.MapGet("/api/public/datasets/{code}/schema", async (string code) =>
{
    var sample = await BuildSampleDataContextAsync();
    var query = new PublicDataSetQueryService(sample.DataSetStore, sample.RecordStore);
    var schema = await query.FindSchemaByCodeAsync(sample.SiteId, code);

    return schema is null ? Results.NotFound() : Results.Ok(schema);
});

app.MapGet("/api/public/datasets/{code}/records/{recordKey}", async (string code, string recordKey) =>
{
    var sample = await BuildSampleDataContextAsync();
    var query = new PublicDataSetQueryService(sample.DataSetStore, sample.RecordStore);
    var record = await query.FindRecordByKeyAsync(sample.SiteId, code, recordKey);

    return record is null ? Results.NotFound() : Results.Ok(record);
});

app.MapGet("/api/public/datasets/{code}/export.csv", async (string code) =>
{
    var sample = await BuildSampleDataContextAsync();
    var query = new PublicDataSetQueryService(sample.DataSetStore, sample.RecordStore);
    var detail = await query.FindByCodeAsync(sample.SiteId, code);

    return detail is null
        ? Results.NotFound()
        : Results.Text(CsvDataExporter.Export(detail.Records), "text/csv");
});

app.MapGet("/api/public/search", async (string q, IContentItemStore contentStore) =>
{
    var index = await BuildSampleSearchIndexAsync(contentStore);
    var results = await index.SearchAsync(new SearchQuery(q));

    return results.Select(result => new
    {
        result.Document.TargetType,
        result.Document.TargetId,
        result.Document.Title,
        result.Document.Summary,
        result.Document.Url,
        result.Score,
        result.MatchedFields
    });
});

app.MapGet("/api/public/search/health", async (IContentItemStore contentStore) =>
{
    var index = await BuildSampleSearchIndexAsync(contentStore);
    var results = await index.SearchAsync(new SearchQuery("sample"));

    return new
    {
        Provider = "in-memory",
        Status = "ready",
        SampleDocumentCount = results.Count
    };
});

app.Run();

static bool IsPublicOutputRequest(HttpRequest request)
{
    if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
    {
        return false;
    }

    return request.Path.StartsWithSegments("/api/public", StringComparison.OrdinalIgnoreCase) ||
        request.Path.StartsWithSegments("/content", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/rss.xml", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/llms.txt", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/llms-full.txt", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/.well-known/agent.json", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/api/openapi.json", StringComparison.OrdinalIgnoreCase);
}

static async Task<AnalyticsEvent> StoreAnalyticsEventAsync(
    HttpContext context,
    string eventType,
    IReadOnlyDictionary<string, string>? metadata = null,
    string? siteId = null,
    string? path = null,
    string? sessionId = null,
    DateTimeOffset? occurredAt = null,
    string? referrer = null,
    string? userAgent = null,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
{
    var analyticsEvent = CreateAnalyticsEvent(
        context,
        eventType,
        metadata,
        siteId,
        path,
        sessionId,
        occurredAt,
        referrer,
        userAgent,
        ipAddress);
    await context.RequestServices
        .GetRequiredService<IAnalyticsEventStore>()
        .AddAsync(analyticsEvent, cancellationToken);

    return analyticsEvent;
}

static AnalyticsEvent CreateAnalyticsEvent(
    HttpContext context,
    string eventType,
    IReadOnlyDictionary<string, string>? metadata = null,
    string? siteId = null,
    string? path = null,
    string? sessionId = null,
    DateTimeOffset? occurredAt = null,
    string? referrer = null,
    string? userAgent = null,
    string? ipAddress = null)
{
    var factory = context.RequestServices.GetRequiredService<AnalyticsEventFactory>();
    return factory.Create(
        string.IsNullOrWhiteSpace(siteId) ? ResolveSiteId(context) : siteId,
        eventType,
        string.IsNullOrWhiteSpace(path) ? context.Request.Path + context.Request.QueryString : path,
        ResolveSessionId(context, sessionId),
        occurredAt ?? DateTimeOffset.UtcNow,
        referrer ?? context.Request.Headers.Referer.ToString(),
        userAgent ?? context.Request.Headers.UserAgent.ToString(),
        ipAddress ?? context.Connection.RemoteIpAddress?.ToString(),
        metadata);
}

static string ResolveSiteId(HttpContext context)
{
    var headerValue = context.Request.Headers["X-OpenPortalKit-Site"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(headerValue))
    {
        return headerValue;
    }

    var configured = context.RequestServices
        .GetRequiredService<IConfiguration>()
        .GetValue<string>("OpenPortalKit:Analytics:SiteId");
    return string.IsNullOrWhiteSpace(configured) ? context.Request.Host.Host : configured;
}

static string ResolveSessionId(HttpContext context, string? explicitSessionId)
{
    if (!string.IsNullOrWhiteSpace(explicitSessionId))
    {
        return explicitSessionId;
    }

    var headerValue = context.Request.Headers["X-OpenPortalKit-Session"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(headerValue))
    {
        return headerValue;
    }

    var userAgent = context.Request.Headers.UserAgent.ToString();
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return remoteIp + ":" + userAgent;
}

static string BuildAnalyticsClientScript()
{
    return """
(() => {
  const endpoint = "/analytics/events";
  const key = "opk_session";
  const site = document.documentElement.getAttribute("data-opk-site") || location.host;
  let session = localStorage.getItem(key);
  if (!session) {
    session = crypto.randomUUID ? crypto.randomUUID() : String(Date.now()) + "." + Math.random();
    localStorage.setItem(key, session);
  }

  const payload = {
    siteId: site,
    eventType: "page_view",
    path: location.pathname + location.search,
    sessionId: session,
    referrer: document.referrer || null,
    userAgent: navigator.userAgent,
    metadata: {
      page_title: document.title || "",
      client_type: "browser"
    }
  };

  if (navigator.sendBeacon) {
    navigator.sendBeacon(endpoint, new Blob([JSON.stringify(payload)], { type: "application/json" }));
    return;
  }

  fetch(endpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
    keepalive: true
  }).catch(() => {});
})();
""";
}

static Uri GetSiteBaseUrl(HttpRequest request)
{
    return new Uri($"{request.Scheme}://{request.Host}");
}

static AgentSiteProfile BuildAgentSiteProfile(HttpRequest request)
{
    var siteBaseUrl = GetSiteBaseUrl(request);

    return new AgentSiteProfile(
        "OpenPortalKit Public Portal",
        "Public publishing outputs for crawlable pages, structured data, machine-readable snapshots, and agent-friendly discovery.",
        siteBaseUrl,
        new[]
        {
            new AgentSection("Content", new Uri(siteBaseUrl, "/api/public/content"), "Published public content with Markdown and JSON snapshots."),
            new AgentSection("Datasets", new Uri(siteBaseUrl, "/api/public/datasets"), "Public structured datasets with schema and traceable records."),
            new AgentSection("Search", new Uri(siteBaseUrl, "/api/public/search"), "Read-only public search across published content and datasets.")
        },
        new[]
        {
            new AgentLink("Public API", new Uri(siteBaseUrl, "/api/public"), "Discovery document for public read endpoints."),
            new AgentLink("Sitemap", new Uri(siteBaseUrl, "/sitemap.xml"), "Crawlable XML sitemap."),
            new AgentLink("RSS feed", new Uri(siteBaseUrl, "/rss.xml"), "Latest public publishing feed."),
            new AgentLink("OpenAPI", new Uri(siteBaseUrl, "/api/openapi.json"), "OpenAPI description for public read endpoints.")
        },
        new Uri(siteBaseUrl, "/sitemap.xml"),
        new Uri(siteBaseUrl, "/rss.xml"),
        new Uri(siteBaseUrl, "/api/public"),
        new Uri(siteBaseUrl, "/api/openapi.json"),
        new Uri(siteBaseUrl, "/llms.txt"),
        new Uri(siteBaseUrl, "/llms-full.txt"),
        new Uri(siteBaseUrl, "/.well-known/agent.json"),
        "Public content and dataset endpoints are read-only. Automated systems should respect robots.txt, attribution requirements, and endpoint rate limits.",
        "When citing content, include the canonical URL and preserve source attribution where provided.");
}

static IReadOnlyList<RobotsDirective> BuildRobotsDirectives(AgentBotPolicy policy)
{
    var directives = new List<RobotsDirective>
    {
        new(
            "*",
            policy.AllowSearchBots ? new[] { "/" } : Array.Empty<string>(),
            policy.AllowSearchBots ? new[] { "/admin", "/api/system" } : new[] { "/" },
            policy.CrawlDelaySeconds)
    };

    foreach (var userAgent in new[] { "GPTBot", "CCBot", "Google-Extended", "anthropic-ai", "ClaudeBot" })
    {
        directives.Add(new RobotsDirective(
            userAgent,
            policy.AllowTrainingBots ? new[] { "/" } : Array.Empty<string>(),
            policy.AllowTrainingBots ? new[] { "/admin", "/api/system" } : new[] { "/" },
            policy.CrawlDelaySeconds));
    }

    foreach (var userAgent in policy.AllowedUserAgents)
    {
        directives.Add(new RobotsDirective(
            userAgent,
            new[] { "/" },
            new[] { "/admin", "/api/system" },
            policy.CrawlDelaySeconds));
    }

    return directives;
}

static Guid GetDefaultSiteId()
{
    return Guid.Parse("22222222-2222-2222-2222-222222222222");
}

static Guid GetDefaultContentTypeId()
{
    return Guid.Parse("33333333-3333-3333-3333-333333333333");
}

static IContentItemStore BuildSeedContentItemStore()
{
    var store = new InMemoryContentItemStore();

    foreach (var item in GetSeedContentItems())
    {
        store.AddAsync(item).GetAwaiter().GetResult();
    }

    return store;
}

static IReadOnlyList<ContentItem> GetSeedContentItems()
{
    var siteId = GetDefaultSiteId();
    var contentTypeId = GetDefaultContentTypeId();
    var actorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    return GetSeedPublicResourceDescriptors()
        .Select((resource, index) => new ContentItem(
            Guid.Parse(index == 0 ? "11111111-1111-1111-1111-111111111111" : "12121212-1212-1212-1212-121212121212"),
            siteId,
            contentTypeId,
            resource.Title,
            resource.Path.Trim('/').Split('/').Last(),
            resource.Description,
            BuildSampleContentBody(resource.Title),
            CoverAssetId: null,
            ContentPublicationStatus.Published,
            CategoryId: null,
            new[] { "publishing", "agent-readiness", "public-output" },
            AuthorId: actorId,
            "OpenPortalKit seeded public content",
            resource.PublishedAt,
            ScheduledAt: null,
            ExpiresAt: null,
            actorId,
            actorId,
            resource.PublishedAt.AddHours(-1),
            resource.UpdatedAt))
        .ToArray();
}

static IReadOnlyList<PublicResourceDescriptor> GetSeedPublicResourceDescriptors()
{
    return new[]
    {
        new PublicResourceDescriptor(
            "OpenPortalKit Launch Notes",
            "A crawlable public content resource for SEO baseline validation.",
            "/content/launch-notes",
            new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero),
            "en-US"),
        new PublicResourceDescriptor(
            "Publishing Health Overview",
            "A public resource used to validate sitemap and RSS generation.",
            "/content/publishing-health-overview",
            new DateTimeOffset(2026, 7, 8, 9, 30, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 8, 10, 30, 0, TimeSpan.Zero),
            "en-US")
    };
}

static async Task<IReadOnlyList<PublicResourceDescriptor>> GetPublicResourceDescriptorsAsync(
    Uri siteBaseUrl,
    IContentItemStore contentStore)
{
    var query = new PublicContentQueryService(contentStore);
    var items = await query.ListPublishedAsync(new ContentListQuery(SiteId: GetDefaultSiteId()));

    return items
        .Select(item => new PublicResourceDescriptor(
            item.Title,
            item.Summary,
            CanonicalUrlBuilder.NormalizePath("/content/" + item.Slug),
            item.PublishedAt,
            item.UpdatedAt,
            "en-US"))
        .ToArray();
}

static async Task<IReadOnlyList<AgentContentDocument>> GetAgentContentDocumentsAsync(
    Uri siteBaseUrl,
    IContentItemStore contentStore)
{
    var query = new PublicContentQueryService(contentStore);
    var summaries = await query.ListPublishedAsync(new ContentListQuery(SiteId: GetDefaultSiteId()));
    var documents = new List<AgentContentDocument>();

    foreach (var summary in summaries)
    {
        var detail = await query.FindPublishedBySlugAsync(summary.SiteId, summary.Slug);
        if (detail is not null)
        {
            documents.Add(BuildAgentContentDocument(detail, siteBaseUrl));
        }
    }

    return documents;
}

static async Task<AgentContentDocument?> FindAgentContentDocumentAsync(
    Uri siteBaseUrl,
    IContentItemStore contentStore,
    string slug)
{
    var query = new PublicContentQueryService(contentStore);
    var detail = await query.FindPublishedBySlugAsync(GetDefaultSiteId(), slug);

    return detail is null ? null : BuildAgentContentDocument(detail, siteBaseUrl);
}

static AgentContentDocument BuildAgentContentDocument(PublicContentDetail detail, Uri siteBaseUrl)
{
    return new AgentContentDocument(
        "content:" + detail.Slug,
        "Article",
        detail.Title,
        detail.Slug,
        detail.Summary,
        detail.Body,
        CanonicalUrlBuilder.Build(siteBaseUrl, "/content/" + detail.Slug),
        detail.PublishedAt,
        detail.UpdatedAt,
        "OpenPortalKit Editorial",
        detail.Source,
        detail.Tags,
        new[]
        {
            "Public outputs include HTML, XML feeds, Markdown snapshots, JSON snapshots, and API descriptions.",
            "Machine-readable resources must preserve canonical URLs and source attribution.",
            "Agent-facing access is governed by robots.txt and the agent manifest."
        },
        new[]
        {
            new AgentLink("Public API discovery", new Uri(siteBaseUrl, "/api/public"), "Read-only endpoint catalog."),
            new AgentLink("llms.txt", new Uri(siteBaseUrl, "/llms.txt"), "Concise LLM discovery file.")
        },
        new[]
        {
            new AgentLink("Sitemap", new Uri(siteBaseUrl, "/sitemap.xml"), "Crawlable public URL inventory."),
            new AgentLink("RSS feed", new Uri(siteBaseUrl, "/rss.xml"), "Public publishing feed.")
        },
        AgentVisibilityPolicy.Default,
        "This public snapshot may be used for search indexing, citation, and retrieval-augmented generation when the canonical URL and source attribution are preserved.");
}

#pragma warning disable CS8321
static IReadOnlyList<PublicResourceDescriptor> GetSamplePublicResourcesLegacy()
{
    return new[]
    {
        new PublicResourceDescriptor(
            "OpenPortalKit Launch Notes",
            "A crawlable sample public content resource for SEO baseline validation.",
            "/content/launch-notes",
            new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero),
            "en-US"),
        new PublicResourceDescriptor(
            "Publishing Health Overview",
            "A sample public resource used to validate sitemap and RSS generation.",
            "/content/publishing-health-overview",
            new DateTimeOffset(2026, 7, 8, 9, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 8, 10, 30, 0, TimeSpan.Zero),
            "en-US")
    };
}

static AgentContentDocument BuildSampleAgentContentDocumentLegacy(PublicResourceDescriptor resource, Uri siteBaseUrl)
{
    var slug = resource.Path.Trim('/').Split('/').Last();
    var canonicalUrl = CanonicalUrlBuilder.Build(siteBaseUrl, resource.Path);
    var source = "OpenPortalKit sample publishing seed";

    return new AgentContentDocument(
        "content:" + slug,
        "Article",
        resource.Title,
        slug,
        resource.Description,
        BuildSampleContentBody(resource.Title),
        canonicalUrl,
        resource.PublishedAt,
        resource.UpdatedAt,
        "OpenPortalKit Editorial",
        source,
        new[] { "publishing", "agent-readiness", "public-output" },
        new[]
        {
            "Public outputs include HTML, XML feeds, Markdown snapshots, JSON snapshots, and API descriptions.",
            "Machine-readable resources must preserve canonical URLs and source attribution.",
            "Agent-facing access is governed by robots.txt and the agent manifest."
        },
        new[]
        {
            new AgentLink("Public API discovery", new Uri(siteBaseUrl, "/api/public"), "Read-only endpoint catalog."),
            new AgentLink("llms.txt", new Uri(siteBaseUrl, "/llms.txt"), "Concise LLM discovery file.")
        },
        new[]
        {
            new AgentLink("Sitemap", new Uri(siteBaseUrl, "/sitemap.xml"), "Crawlable public URL inventory."),
            new AgentLink("RSS feed", new Uri(siteBaseUrl, "/rss.xml"), "Public publishing feed.")
        },
        AgentVisibilityPolicy.Default,
        "This public snapshot may be used for search indexing, citation, and retrieval-augmented generation when the canonical URL and source attribution are preserved.");
}

static string BuildSampleContentBody(string title)
{
    return $"""
    {title} explains how OpenPortalKit publishes content in formats that are usable by people, search engines, LLMs, RAG systems, and browser agents.

    The public output set includes crawlable HTML routes, sitemap and RSS discovery, clean Markdown snapshots, JSON snapshots with traceability fields, and an OpenAPI description for read-only public endpoints.

    The content is designed to be citable. Agents should use canonical URLs, preserve source attribution, and respect robots.txt and the agent manifest before automated collection.
    """;
}

#pragma warning restore CS8321

static IRedirectRuleStore GetSampleRedirectRuleStore()
{
    var now = new DateTimeOffset(2026, 7, 8, 11, 0, 0, TimeSpan.Zero);
    var store = new InMemoryRedirectRuleStore();

    store.AddAsync(new RedirectRule(
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "/news/launch",
        "/content/launch-notes",
        RedirectStatus.Permanent,
        IsEnabled: true,
        now,
        now)).GetAwaiter().GetResult();

    store.AddAsync(new RedirectRule(
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        "/old-health-overview",
        "/content/publishing-health-overview",
        RedirectStatus.Permanent,
        IsEnabled: true,
        now,
        now)).GetAwaiter().GetResult();

    return store;
}

static IReadOnlyList<DataSet> GetSampleDataSets()
{
    var now = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    return new[]
    {
        new DataSet(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            "sample-catalog",
            "Sample Catalog",
            "A generic structured dataset sample with traceable public records.",
            IsPublic: true,
            now,
            now)
    };
}

static async Task<SampleDataContext> BuildSampleDataContextAsync()
{
    var dataSetStore = new InMemoryDataSetStore();
    var recordStore = new InMemoryDataRecordStore();
    var service = new DataImportService(dataSetStore, recordStore);
    var dataSet = GetSampleDataSets()[0];
    var schema = new DataSchemaVersion(
        Guid.Parse("88888888-8888-8888-8888-888888888888"),
        dataSet.Id,
        VersionNumber: 1,
        """{"type":"object","properties":{"name":{"type":"string"},"category":{"type":"string"}}}""",
        DataChecksum.FromJson("""{"type":"object","properties":{"name":{"type":"string"},"category":{"type":"string"}}}"""),
        new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero));

    await dataSetStore.AddDataSetAsync(dataSet);
    await dataSetStore.AddSchemaVersionAsync(schema);
    await service.ImportAsync(new DataImportRequest(
        dataSet.Id,
        schema.Id,
        "sample-seed",
        new DateOnly(2026, 7, 8),
        Guid.Parse("99999999-9999-9999-9999-999999999999"),
        new[]
        {
            new DataImportRow("alpha", """{"name":"Alpha","category":"example"}"""),
            new DataImportRow("beta", """{"name":"Beta","category":"example"}""")
        },
        ImportedAt: new DateTimeOffset(2026, 7, 8, 12, 30, 0, TimeSpan.Zero)));

    return new SampleDataContext(dataSet.SiteId, dataSetStore, recordStore);
}

static async Task<ISearchIndex> BuildSampleSearchIndexAsync(IContentItemStore contentStore)
{
    var index = new InMemorySearchIndex();
    var siteBase = new Uri("https://example.test");
    var samplePublishedAt = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    var publicContent = new PublicContentQueryService(contentStore);
    var contentItems = await publicContent.ListPublishedAsync(new ContentListQuery(SiteId: GetDefaultSiteId()));

    foreach (var item in contentItems)
    {
        await index.UpsertAsync(new SearchDocument(
            "content:" + item.Slug,
            "ContentItem",
            item.Id.ToString(),
            item.Title,
            item.Summary,
            item.Summary,
            CanonicalUrlBuilder.Build(siteBase, "/content/" + item.Slug).PathAndQuery,
            "content",
            item.Tags,
            Category: null,
            item.PublishedAt,
            item.UpdatedAt,
            SearchVisibility.Public,
            "en-US",
            MetadataJson: null));
    }

    var sample = await BuildSampleDataContextAsync();
    var query = new PublicDataSetQueryService(sample.DataSetStore, sample.RecordStore);
    var dataSet = await query.FindByCodeAsync(sample.SiteId, "sample-catalog");

    if (dataSet is not null)
    {
        await index.UpsertAsync(new SearchDocument(
            "dataset:" + dataSet.Code,
            "DataSet",
            dataSet.Code,
            dataSet.Name,
            dataSet.Description,
            string.Join(' ', dataSet.Records.Select(record => record.PayloadJson)),
            "/api/public/datasets/" + dataSet.Code,
            "dataset",
            new[] { "sample", "data" },
            Category: null,
            PublishedAt: samplePublishedAt,
            UpdatedAt: dataSet.Records.Count > 0 ? dataSet.Records.Max(record => record.UpdatedAt) : DateTimeOffset.UtcNow,
            SearchVisibility.Public,
            Language: null,
            MetadataJson: null));
    }

    return index;
}

internal sealed record SampleDataContext(
    Guid SiteId,
    InMemoryDataSetStore DataSetStore,
    InMemoryDataRecordStore RecordStore);

public sealed record AnalyticsEventCaptureRequest(
    string? SiteId,
    string EventType,
    string Path,
    string? SessionId = null,
    DateTimeOffset? OccurredAt = null,
    string? Referrer = null,
    string? UserAgent = null,
    string? IpAddress = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed class ApiAnalyticsCaptureQueue : BackgroundService
{
    private readonly Channel<AnalyticsEvent> _channel;
    private readonly IAnalyticsEventStore _store;
    private readonly AnalyticsPrivacyOptions _options;
    private readonly ILogger<ApiAnalyticsCaptureQueue> _logger;
    private DateTimeOffset _lastPruned = DateTimeOffset.MinValue;

    public ApiAnalyticsCaptureQueue(
        IAnalyticsEventStore store,
        IOptions<AnalyticsPrivacyOptions> options,
        ILogger<ApiAnalyticsCaptureQueue> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _channel = Channel.CreateBounded<AnalyticsEvent>(new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public bool TryEnqueue(AnalyticsEvent analyticsEvent)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);
        return _channel.Writer.TryWrite(analyticsEvent);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var analyticsEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _store.AddAsync(analyticsEvent, stoppingToken);
                await PruneIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Analytics event capture failed.");
            }
        }
    }

    private async Task PruneIfDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastPruned < TimeSpan.FromHours(1))
        {
            return;
        }

        _lastPruned = now;
        await _store.DeleteOlderThanAsync(
            now.AddDays(-Math.Max(1, _options.RetentionDays)),
            cancellationToken);
    }
}
