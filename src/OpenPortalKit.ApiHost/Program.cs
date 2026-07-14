using System.Diagnostics;
using System.Threading.Channels;
using System.Data.Common;
using System.Text;
using System.Text.Encodings.Web;
using Npgsql;
using OpenPortalKit.Kernel.Configuration;
using OpenPortalKit.Kernel.Persistence;
using OpenPortalKit.Infrastructure.Production;
using OpenPortalKit.Modules.AgentAccess;
using OpenPortalKit.Modules.AgentAccess.AgentOutputs;
using OpenPortalKit.Modules.Assets;
using OpenPortalKit.Modules.Audit;
using OpenPortalKit.Modules.Content;
using OpenPortalKit.Modules.Content.BlockTemplates;
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
ProductionConfigurationValidator.ValidateWebHost(builder.Configuration, builder.Environment.IsDevelopment());

builder.Services.AddOpenPortalKitProductionHardening(builder.Configuration, adminHost: false);
var publicCaching = builder.Configuration
    .GetSection(PublicResponseCacheOptions.SectionName)
    .Get<PublicResponseCacheOptions>() ?? new PublicResponseCacheOptions();
publicCaching.Validate();
builder.Services.AddSingleton(publicCaching);
builder.Services.Configure<OpenPortalKitStorageOptions>(
    builder.Configuration.GetSection(OpenPortalKitStorageOptions.SectionName));
builder.Services.Configure<AnalyticsPrivacyOptions>(
    builder.Configuration.GetSection(AnalyticsPrivacyOptions.SectionName));
builder.Services.Configure<AgentBotPolicyOptions>(
    builder.Configuration.GetSection(AgentBotPolicyOptions.SectionName));
var persistencePostgres = builder.Configuration
    .GetSection(PostgresPersistenceOptions.SectionName)
    .Get<PostgresPersistenceOptions>() ?? new PostgresPersistenceOptions();
if (string.IsNullOrWhiteSpace(persistencePostgres.ConnectionString))
{
    persistencePostgres.ConnectionString = builder.Configuration.GetConnectionString(
        persistencePostgres.ConnectionStringName);
}
if (persistencePostgres.Enabled)
{
    if (string.IsNullOrWhiteSpace(persistencePostgres.ConnectionString))
    {
        throw new InvalidOperationException("PostgreSQL persistence is enabled without a connection string.");
    }
    builder.Services.AddDatabaseReadinessCheck(
        persistencePostgres.ProviderInvariantName,
        persistencePostgres.ConnectionString);
}
var configuredStorage = builder.Configuration
    .GetSection(OpenPortalKitStorageOptions.SectionName)
    .Get<OpenPortalKitStorageOptions>() ?? new OpenPortalKitStorageOptions();
var redisConnection = builder.Configuration.GetConnectionString(configuredStorage.CacheConnectionStringName);
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddRedisReadinessCheck(redisConnection);
}

builder.Services.AddSingleton(persistencePostgres);
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
builder.Services.AddSingleton<IPageBlockDataResolver, ApiPublicPageBlockDataResolver>();
if (persistencePostgres.Enabled)
{
    builder.Services.AddSingleton<IOpenPortalKitDbConnectionFactory, PostgresOpenPortalKitDbConnectionFactory>();
    builder.Services.AddSingleton<IContentItemStore, PostgresContentItemStore>();
    builder.Services.AddSingleton<IPageStore, PostgresPageStore>();
    builder.Services.AddSingleton<IDataSetStore, PostgresDataSetStore>();
    builder.Services.AddSingleton<IDataRecordStore, PostgresDataRecordStore>();
}
else
{
    builder.Services.AddSingleton<IContentItemStore>(_ => BuildSeedContentItemStore());
    builder.Services.AddSingleton<IPageStore>(_ => BuildSeedPageStore());
    builder.Services.AddSingleton(_ => BuildSampleDataContextAsync().GetAwaiter().GetResult());
    builder.Services.AddSingleton<IDataSetStore>(provider => provider.GetRequiredService<SampleDataContext>().DataSetStore);
    builder.Services.AddSingleton<IDataRecordStore>(provider => provider.GetRequiredService<SampleDataContext>().RecordStore);
}
builder.Services.AddSingleton<ServerRenderedBlockPageRenderer>();

var app = builder.Build();

app.UseRouting();
app.UseOpenPortalKitProductionHardening();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/public") ||
        context.Request.Path.Equals("/api/openapi.json", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers[PublicApiContract.VersionHeaderName] = PublicApiContract.Version;
    }

    await next();
});

app.Use(async (context, next) =>
{
    var options = context.RequestServices.GetRequiredService<PublicResponseCacheOptions>();
    if (!options.Enabled || !IsPublicOutputRequest(context.Request))
    {
        await next();
        return;
    }

    context.Response.OnStarting(() =>
    {
        if (context.Response.StatusCode is >= 200 and < 300 &&
            !context.Response.Headers.ContainsKey("Cache-Control") &&
            !context.Response.Headers.ContainsKey("Set-Cookie"))
        {
            context.Response.Headers.CacheControl =
                $"public, max-age={options.BrowserMaxAgeSeconds}, s-maxage={options.SharedMaxAgeSeconds}, stale-while-revalidate={options.StaleWhileRevalidateSeconds}";
        }
        return Task.CompletedTask;
    });
    await next();
});

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
app.MapOpenPortalKitHealthEndpoints();

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

app.MapGet("/sitemap.xml", async (HttpRequest request, IContentItemStore contentStore, IPageStore pageStore) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var resources = await GetPublicResourceDescriptorsAsync(siteBaseUrl, contentStore, pageStore);
    var entries = resources
        .Select(resource => new SitemapEntry(
            CanonicalUrlBuilder.Build(siteBaseUrl, resource.Path),
            resource.UpdatedAt,
            SitemapChangeFrequency.Weekly,
            0.7m));

    return Results.Text(SitemapXmlGenerator.Generate(entries), "application/xml");
});

app.MapGet("/rss.xml", async (HttpRequest request, IContentItemStore contentStore, IPageStore pageStore) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var resources = await GetPublicResourceDescriptorsAsync(siteBaseUrl, contentStore, pageStore);
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

app.MapGet("/llms.txt", async (HttpRequest request, IContentItemStore contentStore, IPageStore pageStore, ServerRenderedBlockPageRenderer renderer) =>
{
    var profile = BuildAgentSiteProfile(request);
    var documents = await GetAgentContentDocumentsAsync(profile.BaseUrl, contentStore, pageStore, renderer);

    return Results.Text(LlmsTextGenerator.Generate(profile, documents, includeFullContent: false), "text/plain; charset=utf-8");
});

app.MapGet("/llms-full.txt", async (HttpRequest request, IContentItemStore contentStore, IPageStore pageStore, ServerRenderedBlockPageRenderer renderer) =>
{
    var profile = BuildAgentSiteProfile(request);
    var documents = await GetAgentContentDocumentsAsync(profile.BaseUrl, contentStore, pageStore, renderer);

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

app.MapGet("/api/public/content", async (
    HttpRequest request,
    IContentItemStore contentStore,
    int offset = 0,
    int limit = 20) =>
{
    if (!TryValidatePage(offset, limit, out var pageError)) return pageError;
    var siteBaseUrl = GetSiteBaseUrl(request);
    var documents = await GetAgentContentDocumentsAsync(
        siteBaseUrl,
        contentStore,
        query: new ContentListQuery(SiteId: GetDefaultSiteId(), Skip: offset, Take: limit + 1));

    var items = documents.Take(limit)
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
        }).ToArray();
    return Results.Ok(new PublicPage<object>(items, offset, limit, documents.Count > limit));
});

app.MapGet("/api/public/pages", async (
    HttpRequest request,
    IPageStore pageStore,
    int offset = 0,
    int limit = 20) =>
{
    if (!TryValidatePage(offset, limit, out var pageError)) return pageError;
    var siteBaseUrl = GetSiteBaseUrl(request);
    var pages = await new PublicPageQueryService(pageStore)
        .ListPublishedPageAsync(GetDefaultSiteId(), offset, limit + 1);
    var items = pages.Take(limit).Select(page => new
    {
        page.Id,
        page.Title,
        page.Slug,
        page.Summary,
        CanonicalUrl = new Uri(siteBaseUrl, "/pages/" + page.Slug),
        MarkdownSnapshot = new Uri(siteBaseUrl, "/pages/" + page.Slug + ".md"),
        JsonSnapshot = new Uri(siteBaseUrl, "/api/public/pages/" + page.Slug + ".json"),
        page.PublishedAt,
        page.UpdatedAt,
        page.Revision
    }).ToArray();
    return Results.Ok(new PublicPage<object>(items, offset, limit, pages.Count > limit));
});

app.MapGet("/content/{slug}", async (string slug, HttpRequest request, IContentItemStore contentStore) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var document = await FindAgentContentDocumentAsync(siteBaseUrl, contentStore, slug);
    if (document is null) return Results.NotFound();
    if (ApplyConditionalHeaders(request, "content-html:" + document.Id, document.UpdatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);

    var metadata = SeoPageMetadataBuilder.Build(
        new PublicResourceDescriptor(
            document.Title,
            document.Summary,
            "/content/" + document.Slug,
            document.PublishedAt,
            document.UpdatedAt,
            "en-US"),
        siteBaseUrl,
        "OpenPortalKit");
    return Results.Content(BuildPublicContentHtml(document, metadata), "text/html; charset=utf-8");
});

app.MapGet("/content/{slug}.md", async (string slug, HttpRequest request, IContentItemStore contentStore) =>
{
    var document = await FindAgentContentDocumentAsync(GetSiteBaseUrl(request), contentStore, slug);
    if (document is null) return Results.NotFound();
    if (ApplyConditionalHeaders(request, "content-markdown:" + document.Id, document.UpdatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Text(AgentSnapshotGenerator.GenerateMarkdown(document), "text/markdown; charset=utf-8");
});

app.MapGet("/api/public/content/{slug}.json", async (string slug, HttpRequest request, IContentItemStore contentStore) =>
{
    var document = await FindAgentContentDocumentAsync(GetSiteBaseUrl(request), contentStore, slug);
    if (document is null) return Results.NotFound();
    if (ApplyConditionalHeaders(request, "content-json:" + document.Id, document.UpdatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Text(AgentSnapshotGenerator.GenerateJson(document), "application/json; charset=utf-8");
});

app.MapGet("/pages/{slug}", async (
    string slug,
    HttpRequest request,
    IPageStore pageStore,
    ServerRenderedBlockPageRenderer renderer) =>
{
    var page = await new PublicPageQueryService(pageStore)
        .FindPublishedBySlugAsync(GetDefaultSiteId(), slug);
    if (page is null)
    {
        return Results.NotFound();
    }

    var siteBaseUrl = GetSiteBaseUrl(request);
    var metadata = SeoPageMetadataBuilder.Build(
        new PublicResourceDescriptor(
            page.Title,
            page.Summary,
            "/pages/" + page.Slug,
            page.PublishedAt!.Value,
            page.UpdatedAt,
            "en-US"),
        siteBaseUrl,
        "OpenPortalKit",
        "WebPage");
    if (ApplyConditionalHeaders(request, $"page-html:{page.Id}:{page.Revision}", page.UpdatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    var html = BuildPublicPageHtml(page, metadata, await renderer.RenderBodyAsync(page));
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/pages/{slug}.md", async (
    string slug,
    HttpRequest request,
    IPageStore pageStore,
    ServerRenderedBlockPageRenderer renderer) =>
{
    var page = await new PublicPageQueryService(pageStore)
        .FindPublishedBySlugAsync(GetDefaultSiteId(), slug);
    if (page is null) return Results.NotFound();
    if (ApplyConditionalHeaders(request, $"page-markdown:{page.Id}:{page.Revision}", page.UpdatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Text(AgentSnapshotGenerator.GenerateMarkdown(
        await BuildAgentPageDocumentAsync(page, GetSiteBaseUrl(request), renderer)), "text/markdown; charset=utf-8");
});

app.MapGet("/api/public/pages/{slug}.json", async (
    string slug,
    HttpRequest request,
    IPageStore pageStore,
    ServerRenderedBlockPageRenderer renderer) =>
{
    var page = await new PublicPageQueryService(pageStore)
        .FindPublishedBySlugAsync(GetDefaultSiteId(), slug);
    if (page is null) return Results.NotFound();
    if (ApplyConditionalHeaders(request, $"page-json:{page.Id}:{page.Revision}", page.UpdatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Text(AgentSnapshotGenerator.GenerateJson(
        await BuildAgentPageDocumentAsync(page, GetSiteBaseUrl(request), renderer)), "application/json; charset=utf-8");
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

app.MapGet("/api/public/datasets", async (IDataSetStore dataSetStore, IDataRecordStore recordStore) =>
    Results.Ok(await new PublicDataSetQueryService(dataSetStore, recordStore)
        .ListPublicAsync(GetDefaultSiteId())));

app.MapGet("/api/public/datasets/{code}", async (
    string code,
    HttpRequest request,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore) =>
{
    var query = new PublicDataSetQueryService(dataSetStore, recordStore);
    var detail = await query.FindByCodeAsync(GetDefaultSiteId(), code);
    if (detail is null) return Results.NotFound();
    var updatedAt = detail.Records.Count == 0 ? DateTimeOffset.UnixEpoch : detail.Records.Max(record => record.UpdatedAt);
    var version = "dataset:" + detail.Code + ":" + string.Join(':', detail.Records.Select(record => record.Checksum));
    if (ApplyConditionalHeaders(request, version, updatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Ok(detail);
});

app.MapGet("/api/public/datasets/{code}/records", async (
    string code,
    HttpRequest request,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore,
    int offset = 0,
    int limit = 50) =>
{
    if (!TryValidatePage(offset, limit, out var pageError)) return pageError;
    var query = new PublicDataSetQueryService(dataSetStore, recordStore);
    var detail = await query.FindByCodeAsync(GetDefaultSiteId(), code);

    if (detail is null) return Results.NotFound();
    var records = detail.Records.Skip(offset).Take(limit + 1).ToArray();
    var pageItems = records.Take(limit).ToArray();
    var updatedAt = pageItems.Length == 0 ? DateTimeOffset.UnixEpoch : pageItems.Max(record => record.UpdatedAt);
    var version = $"dataset-records:{code}:{offset}:{limit}:" + string.Join(':', pageItems.Select(record => record.Checksum));
    if (ApplyConditionalHeaders(request, version, updatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Ok(new PublicPage<PublicDataRecord>(pageItems, offset, limit, records.Length > limit));
});

app.MapGet("/api/public/datasets/{code}/schema", async (
    string code,
    HttpRequest request,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore) =>
{
    var query = new PublicDataSetQueryService(dataSetStore, recordStore);
    var schema = await query.FindSchemaByCodeAsync(GetDefaultSiteId(), code);

    if (schema is null) return Results.NotFound();
    if (ApplyConditionalHeaders(request, $"dataset-schema:{schema.DataSetCode}:{schema.Checksum}", schema.CreatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Ok(schema);
});

app.MapGet("/api/public/datasets/{code}/records/{recordKey}", async (
    string code,
    string recordKey,
    HttpRequest request,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore) =>
{
    var query = new PublicDataSetQueryService(dataSetStore, recordStore);
    var record = await query.FindRecordByKeyAsync(GetDefaultSiteId(), code, recordKey);

    if (record is null) return Results.NotFound();
    if (ApplyConditionalHeaders(request, $"dataset-record:{code}:{record.RecordKey}:{record.Checksum}", record.UpdatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Ok(record);
});

app.MapGet("/api/public/datasets/{code}/export.csv", async (
    string code,
    HttpRequest request,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore) =>
{
    var query = new PublicDataSetQueryService(dataSetStore, recordStore);
    var detail = await query.FindByCodeAsync(GetDefaultSiteId(), code);

    if (detail is null) return Results.NotFound();
    var updatedAt = detail.Records.Count == 0 ? DateTimeOffset.UnixEpoch : detail.Records.Max(record => record.UpdatedAt);
    var version = "dataset-csv:" + detail.Code + ":" + string.Join(':', detail.Records.Select(record => record.Checksum));
    if (ApplyConditionalHeaders(request, version, updatedAt))
        return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Text(CsvDataExporter.Export(detail.Records), "text/csv");
});

app.MapGet("/api/public/search", async (
    string q,
    IContentItemStore contentStore,
    IPageStore pageStore,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore,
    int offset = 0,
    int limit = 20,
    CancellationToken cancellationToken = default) =>
{
    if (!TryValidatePage(offset, limit, out var pageError)) return pageError;
    if (string.IsNullOrWhiteSpace(q) || q.Length > 200)
    {
        return Results.BadRequest(new { Error = "q must contain between 1 and 200 characters." });
    }
    var index = await BuildSearchIndexAsync(contentStore, pageStore, dataSetStore, recordStore, cancellationToken);
    var results = await index.SearchAsync(
        new SearchQuery(q, Limit: limit + 1, Offset: offset), cancellationToken);

    var items = results.Take(limit).Select(result => new
    {
        result.Document.TargetType,
        result.Document.TargetId,
        result.Document.Title,
        result.Document.Summary,
        result.Document.Url,
        result.Score,
        result.MatchedFields
    }).ToArray();
    return Results.Ok(new PublicPage<object>(items, offset, limit, results.Count > limit));
});

app.MapGet("/api/public/search/health", async (
    IContentItemStore contentStore,
    IPageStore pageStore,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore,
    CancellationToken cancellationToken) =>
{
    var index = await BuildSearchIndexAsync(contentStore, pageStore, dataSetStore, recordStore, cancellationToken);
    var results = await index.SearchAsync(new SearchQuery("sample"), cancellationToken);

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
        request.Path.StartsWithSegments("/pages", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/rss.xml", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/llms.txt", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/llms-full.txt", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/.well-known/agent.json", StringComparison.OrdinalIgnoreCase) ||
        request.Path.Equals("/api/openapi.json", StringComparison.OrdinalIgnoreCase);
}

static bool TryValidatePage(int offset, int limit, out IResult error)
{
    if (offset < 0 || limit is < 1 or > 100)
    {
        error = Results.BadRequest(new { Error = "offset must be non-negative and limit must be between 1 and 100." });
        return false;
    }

    error = Results.Empty;
    return true;
}

static bool ApplyConditionalHeaders(HttpRequest request, string resourceVersion, DateTimeOffset updatedAt)
{
    var normalizedUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(updatedAt.ToUnixTimeSeconds());
    var versionBytes = System.Text.Encoding.UTF8.GetBytes(
        resourceVersion + ":" + normalizedUpdatedAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
    var entityTag = '"' + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(versionBytes)).ToLowerInvariant() + '"';
    request.HttpContext.Response.Headers.ETag = entityTag;
    request.HttpContext.Response.Headers.LastModified = normalizedUpdatedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    var ifNoneMatch = request.Headers.IfNoneMatch.ToString();
    if (!string.IsNullOrWhiteSpace(ifNoneMatch))
    {
        return ifNoneMatch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => candidate == "*" ||
                string.Equals(candidate.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ? candidate[2..] : candidate,
                    entityTag, StringComparison.Ordinal));
    }

    return DateTimeOffset.TryParse(
            request.Headers.IfModifiedSince.ToString(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var ifModifiedSince) &&
        normalizedUpdatedAt <= ifModifiedSince.ToUniversalTime();
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

static IPageStore BuildSeedPageStore()
{
    var store = new InMemoryPageStore();
    var now = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
    var page = new PortalPage(
        Guid.Parse("a1000000-0000-0000-0000-000000000001"),
        GetDefaultSiteId(),
        Guid.Parse("a1000000-0000-0000-0000-000000000002"),
        1,
        "OpenPortalKit Public Pages",
        "public-pages",
        "A server-rendered page assembled from predefined, versioned blocks.",
        PortalPageStatus.Published,
        new[]
        {
            new BlockInstance(
                Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                "hero",
                "block.hero.v1",
                0,
                """{"headline":"Public pages, without a page builder","summary":"OpenPortalKit renders versioned templates on the server with constrained block configuration.","actionUrl":"/api/public","actionLabel":"Explore public APIs"}"""),
            new BlockInstance(
                Guid.Parse("a1000000-0000-0000-0000-000000000004"),
                "rich-text",
                "block.rich-text.v1",
                1,
                """{"body":"Templates use predefined blocks with explicit schemas.\nEach page fixes the template version used to create it, preserving traceability for editorial and public output changes."}"""),
            new BlockInstance(
                Guid.Parse("a1000000-0000-0000-0000-000000000006"),
                "content-list",
                "block.content-list.v1",
                2,
                """{"heading":"Latest publishing notes","query":"*","take":3}"""),
            new BlockInstance(
                Guid.Parse("a1000000-0000-0000-0000-000000000007"),
                "data-table",
                "block.data-table.v1",
                3,
                """{"heading":"Sample public data","dataSet":"sample-catalog","take":10}""")
        },
        Guid.Parse("a1000000-0000-0000-0000-000000000005"),
        Guid.Parse("a1000000-0000-0000-0000-000000000005"),
        now,
        now,
        now);

    store.UpsertAsync(page).GetAwaiter().GetResult();
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
    IContentItemStore contentStore,
    IPageStore pageStore)
{
    var query = new PublicContentQueryService(contentStore);
    var items = await query.ListPublishedAsync(new ContentListQuery(SiteId: GetDefaultSiteId()));
    var pages = await new PublicPageQueryService(pageStore).ListPublishedAsync(GetDefaultSiteId());

    return items
        .Select(item => new PublicResourceDescriptor(
            item.Title,
            item.Summary,
            CanonicalUrlBuilder.NormalizePath("/content/" + item.Slug),
            item.PublishedAt,
            item.UpdatedAt,
            "en-US"))
        .Concat(pages.Select(page => new PublicResourceDescriptor(
            page.Title,
            page.Summary,
            CanonicalUrlBuilder.NormalizePath("/pages/" + page.Slug),
            page.PublishedAt!.Value,
            page.UpdatedAt,
            "en-US")))
        .ToArray();
}

static string BuildPublicContentHtml(AgentContentDocument document, SeoPageMetadata metadata)
{
    var encoder = HtmlEncoder.Default;
    var body = string.Join(
        Environment.NewLine,
        document.Body.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(paragraph => $"<p>{encoder.Encode(paragraph)}</p>"));
    var source = string.IsNullOrWhiteSpace(document.Source)
        ? string.Empty
        : $"<p class=\"opk-page-data-source\"><strong>Source:</strong> {encoder.Encode(document.Source)}</p>";

    return $"""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            {BuildMetadataHtml(metadata)}
            <style>{BuildPublicPageStyles()}</style>
        </head>
        <body>
            <header class="opk-public-header"><div>OpenPortalKit</div></header>
            <main>
                <article>
                    <header class="opk-page-hero">
                        <h1>{encoder.Encode(document.Title)}</h1>
                        <p>{encoder.Encode(document.Summary)}</p>
                        <time datetime="{encoder.Encode(document.PublishedAt.ToString("O"))}">Published {encoder.Encode(document.PublishedAt.ToString("yyyy-MM-dd"))}</time>
                    </header>
                    <section aria-label="Article content">{body}{source}</section>
                </article>
            </main>
            <footer class="opk-public-footer"><div>Canonical, versioned public content from OpenPortalKit.</div></footer>
        </body>
        </html>
        """;
}

static string BuildPublicPageHtml(PortalPage page, SeoPageMetadata metadata, string body)
{
    return $"""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            {BuildMetadataHtml(metadata)}
            <style>{BuildPublicPageStyles()}</style>
        </head>
        <body>
            <header class="opk-public-header"><div>OpenPortalKit</div></header>
            <main>{body}</main>
            <footer class="opk-public-footer"><div>Published from a versioned OpenPortalKit page template.</div></footer>
        </body>
        </html>
        """;
}

static string BuildMetadataHtml(SeoPageMetadata metadata)
{
    var encoder = HtmlEncoder.Default;
    var builder = new StringBuilder();
    builder.Append("<title>").Append(encoder.Encode(metadata.Title)).AppendLine("</title>");
    builder.Append("<meta name=\"description\" content=\"")
        .Append(encoder.Encode(metadata.Description)).AppendLine("\">");
    builder.Append("<link rel=\"canonical\" href=\"")
        .Append(encoder.Encode(metadata.CanonicalUrl.ToString())).AppendLine("\">");
    foreach (var item in metadata.OpenGraph.OrderBy(item => item.Key, StringComparer.Ordinal))
    {
        builder.Append("<meta property=\"").Append(encoder.Encode(item.Key)).Append("\" content=\"")
            .Append(encoder.Encode(item.Value)).AppendLine("\">");
    }
    builder.Append("<script type=\"application/ld+json\">").Append(metadata.JsonLd).AppendLine("</script>");
    return builder.ToString();
}

static string BuildPublicPageStyles()
{
    return """
        :root { color-scheme: light; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; color: #1b2533; background: #f4f7f8; }
        * { box-sizing: border-box; }
        body { margin: 0; line-height: 1.55; }
        .opk-public-header { border-bottom: 1px solid #d9e1e6; background: #ffffff; }
        .opk-public-header div, main, .opk-public-footer div { width: min(1120px, calc(100% - 40px)); margin: 0 auto; }
        .opk-public-header div { display: flex; align-items: center; min-height: 62px; font-size: 1rem; font-weight: 750; }
        main { padding: 28px 0 56px; }
        section { margin: 0; padding: 30px 28px; border-top: 1px solid #d9e1e6; }
        h1, h2, p { margin-top: 0; }
        h1 { max-width: 760px; margin-bottom: 12px; font-size: clamp(2rem, 5vw, 3.35rem); line-height: 1.08; }
        h2 { margin-bottom: 14px; font-size: 1.25rem; }
        .opk-page-hero { margin-bottom: 8px; border-top: 4px solid #0f766e; background: #e9f7f4; }
        .opk-page-hero p { max-width: 680px; color: #405363; font-size: 1.1rem; }
        a { color: #075f5a; font-weight: 650; }
        .opk-page-hero a { display: inline-block; padding: 9px 13px; background: #0f766e; color: #ffffff; text-decoration: none; }
        .opk-page-list ul, .opk-page-link-list ul, .opk-page-download-list ul, .opk-page-chart ul { display: grid; gap: 12px; margin: 0; padding: 0; list-style: none; }
        .opk-page-list li { padding-bottom: 12px; border-bottom: 1px solid #e4eaed; }
        .opk-page-list li:last-child { padding-bottom: 0; border-bottom: 0; }
        .opk-page-list p, .opk-page-download-list p { margin: 4px 0; color: #526575; }
        time { color: #647785; font-size: .85rem; }
        .opk-page-table-wrap { overflow-x: auto; }
        table { width: 100%; border-collapse: collapse; font-size: .92rem; }
        th, td { padding: 10px 12px; border-bottom: 1px solid #d9e1e6; text-align: left; vertical-align: top; }
        th { color: #405363; background: #eef4f5; font-weight: 750; white-space: nowrap; }
        .opk-page-data-source { display: inline-block; margin-top: 14px; }
        .opk-page-chart li { display: grid; grid-template-columns: minmax(120px, .4fr) minmax(120px, 1fr) auto; gap: 12px; align-items: center; }
        meter { width: 100%; accent-color: #0f766e; }
        details { padding: 12px 0; border-bottom: 1px solid #e4eaed; }
        details:last-child { border-bottom: 0; }
        summary { cursor: pointer; font-weight: 700; }
        address { display: grid; gap: 5px; font-style: normal; }
        iframe { width: 100%; min-height: 420px; border: 1px solid #d9e1e6; }
        .opk-public-footer { padding: 24px 0; border-top: 1px solid #d9e1e6; color: #526575; font-size: .9rem; }
        @media (max-width: 640px) { .opk-public-header div, main, .opk-public-footer div { width: min(100% - 28px, 1120px); } main { padding-top: 14px; } section { padding: 20px; } .opk-page-chart li { grid-template-columns: 1fr; gap: 5px; } iframe { min-height: 300px; } }
        """;
}

static async Task<IReadOnlyList<AgentContentDocument>> GetAgentContentDocumentsAsync(
    Uri siteBaseUrl,
    IContentItemStore contentStore,
    IPageStore? pageStore = null,
    ServerRenderedBlockPageRenderer? pageRenderer = null,
    ContentListQuery? query = null)
{
    var contentQuery = new PublicContentQueryService(contentStore);
    var details = await contentQuery.ListPublishedDetailsAsync(
        query ?? new ContentListQuery(SiteId: GetDefaultSiteId()));
    var documents = new List<AgentContentDocument>();

    foreach (var detail in details)
    {
        documents.Add(BuildAgentContentDocument(detail, siteBaseUrl));
    }

    if (pageStore is not null && pageRenderer is not null)
    {
        var pages = await new PublicPageQueryService(pageStore).ListPublishedAsync(GetDefaultSiteId());
        foreach (var page in pages)
        {
            documents.Add(await BuildAgentPageDocumentAsync(page, siteBaseUrl, pageRenderer));
        }
    }

    return documents;
}

static async Task<AgentContentDocument> BuildAgentPageDocumentAsync(
    PortalPage page,
    Uri siteBaseUrl,
    ServerRenderedBlockPageRenderer renderer)
{
    var text = System.Text.RegularExpressions.Regex.Replace(await renderer.RenderBodyAsync(page), "<[^>]+>", " ");
    text = System.Net.WebUtility.HtmlDecode(text).Trim();

    return new AgentContentDocument(
        "page:" + page.Id.ToString("D"),
        "Page",
        page.Title,
        page.Slug,
        page.Summary,
        text,
        CanonicalUrlBuilder.Build(siteBaseUrl, "/pages/" + page.Slug),
        page.PublishedAt!.Value,
        page.UpdatedAt,
        "OpenPortalKit Editorial",
        "Block Template System",
        new[] { "page", "block-template", "server-rendered" },
        new[]
        {
            "The page is rendered from a fixed, versioned template snapshot.",
            "Block configuration is schema-validated and server-rendered."
        },
        new[] { new AgentLink("Public API discovery", new Uri(siteBaseUrl, "/api/public")) },
        new[] { new AgentLink("Sitemap", new Uri(siteBaseUrl, "/sitemap.xml")) },
        AgentVisibilityPolicy.Default,
        "Cite the canonical URL and preserve source attribution.");
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
            GetDefaultSiteId(),
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

static async Task<ISearchIndex> BuildSearchIndexAsync(
    IContentItemStore contentStore,
    IPageStore pageStore,
    IDataSetStore dataSetStore,
    IDataRecordStore recordStore,
    CancellationToken cancellationToken = default)
{
    const int batchSize = 100;
    const int maximumDocumentsPerType = 1_000;
    var index = new InMemorySearchIndex();
    var siteBase = new Uri("https://example.test");
    var publicContent = new PublicContentQueryService(contentStore);
    for (var skip = 0; skip < maximumDocumentsPerType; skip += batchSize)
    {
        var contentItems = await publicContent.ListPublishedAsync(
            new ContentListQuery(SiteId: GetDefaultSiteId(), Skip: skip, Take: batchSize),
            cancellationToken: cancellationToken);
        foreach (var item in contentItems)
        {
            await index.UpsertAsync(new SearchDocument(
                "content:" + item.Slug, "ContentItem", item.Id.ToString(), item.Title, item.Summary, item.Summary,
                CanonicalUrlBuilder.Build(siteBase, "/content/" + item.Slug).PathAndQuery,
                "content", item.Tags, Category: null, item.PublishedAt, item.UpdatedAt,
                SearchVisibility.Public, "en-US", MetadataJson: null), cancellationToken);
        }
        if (contentItems.Count < batchSize) break;
    }

    var publicPages = new PublicPageQueryService(pageStore);
    for (var skip = 0; skip < maximumDocumentsPerType; skip += batchSize)
    {
        var pages = await publicPages.ListPublishedPageAsync(
            GetDefaultSiteId(), skip, batchSize, cancellationToken: cancellationToken);
        foreach (var page in pages)
        {
            await index.UpsertAsync(new SearchDocument(
                "page:" + page.Slug, "PortalPage", page.Id.ToString(), page.Title, page.Summary, page.Summary,
                "/pages/" + page.Slug, "page", Array.Empty<string>(), Category: null, page.PublishedAt,
                page.UpdatedAt, SearchVisibility.Public, "en-US", MetadataJson: null), cancellationToken);
        }
        if (pages.Count < batchSize) break;
    }

    var query = new PublicDataSetQueryService(dataSetStore, recordStore);
    var dataSets = await query.ListPublicAsync(GetDefaultSiteId(), cancellationToken);
    foreach (var summary in dataSets)
    {
        await index.UpsertAsync(new SearchDocument(
            "dataset:" + summary.Code,
            "DataSet",
            summary.Code,
            summary.Name,
            summary.Description,
            summary.Description,
            "/api/public/datasets/" + summary.Code,
            "dataset",
            new[] { "dataset", summary.Code },
            Category: null,
            PublishedAt: summary.UpdatedAt,
            UpdatedAt: summary.UpdatedAt,
            SearchVisibility.Public,
            Language: null,
            MetadataJson: null), cancellationToken);
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

public sealed record PublicPage<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    bool HasMore);

public sealed class PublicResponseCacheOptions
{
    public const string SectionName = "OpenPortalKit:PublicCaching";
    public bool Enabled { get; init; } = true;
    public int BrowserMaxAgeSeconds { get; init; } = 60;
    public int SharedMaxAgeSeconds { get; init; } = 300;
    public int StaleWhileRevalidateSeconds { get; init; } = 30;

    public void Validate()
    {
        if (BrowserMaxAgeSeconds is < 0 or > 86400 ||
            SharedMaxAgeSeconds is < 0 or > 604800 ||
            StaleWhileRevalidateSeconds is < 0 or > 86400)
        {
            throw new InvalidOperationException(
                "Public caching durations are outside their supported bounds.");
        }
    }
}

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
