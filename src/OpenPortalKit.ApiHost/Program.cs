using OpenPortalKit.Kernel.Configuration;
using OpenPortalKit.Modules.AgentAccess;
using OpenPortalKit.Modules.Assets;
using OpenPortalKit.Modules.Audit;
using OpenPortalKit.Modules.Content;
using OpenPortalKit.Modules.Dashboard;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.Configure<OpenPortalKitStorageOptions>(
    builder.Configuration.GetSection(OpenPortalKitStorageOptions.SectionName));

var app = builder.Build();

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

app.MapGet("/api/public", () => new
{
    Name = "OpenPortalKit Public API",
    Status = "initialized",
    Planned = new[]
    {
        "content",
        "datasets",
        "search",
        "sitemap",
        "rss",
        "markdown-snapshots",
        "json-snapshots",
        "openapi"
    }
});

app.MapGet("/robots.txt", (HttpRequest request) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var policy = new RobotsPolicy(
        new Uri(siteBaseUrl, "/sitemap.xml"),
        new[]
        {
            new RobotsDirective("*", new[] { "/" }, new[] { "/admin", "/api/system" })
        });

    return Results.Text(RobotsTxtGenerator.Generate(policy), "text/plain");
});

app.MapGet("/sitemap.xml", (HttpRequest request) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var entries = GetSamplePublicResources()
        .Select(resource => new SitemapEntry(
            CanonicalUrlBuilder.Build(siteBaseUrl, resource.Path),
            resource.UpdatedAt,
            SitemapChangeFrequency.Weekly,
            0.7m));

    return Results.Text(SitemapXmlGenerator.Generate(entries), "application/xml");
});

app.MapGet("/rss.xml", (HttpRequest request) =>
{
    var siteBaseUrl = GetSiteBaseUrl(request);
    var resources = GetSamplePublicResources();
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

app.MapGet("/api/public/content/sample/metadata", (HttpRequest request) =>
{
    var resource = GetSamplePublicResources()[0];
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

app.MapGet("/api/public/search", async (string q) =>
{
    var index = await BuildSampleSearchIndexAsync();
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

app.MapGet("/api/public/search/health", async () =>
{
    var index = await BuildSampleSearchIndexAsync();
    var results = await index.SearchAsync(new SearchQuery("sample"));

    return new
    {
        Provider = "in-memory",
        Status = "ready",
        SampleDocumentCount = results.Count
    };
});

app.Run();

static Uri GetSiteBaseUrl(HttpRequest request)
{
    return new Uri($"{request.Scheme}://{request.Host}");
}

static IReadOnlyList<PublicResourceDescriptor> GetSamplePublicResources()
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

static async Task<ISearchIndex> BuildSampleSearchIndexAsync()
{
    var index = new InMemorySearchIndex();
    var siteBase = new Uri("https://example.test");
    var samplePublishedAt = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    foreach (var resource in GetSamplePublicResources())
    {
        await index.UpsertAsync(new SearchDocument(
            "content:" + resource.Path.Trim('/'),
            "ContentItem",
            resource.Path.Trim('/'),
            resource.Title,
            resource.Description,
            resource.Description,
            CanonicalUrlBuilder.Build(siteBase, resource.Path).PathAndQuery,
            "content",
            Array.Empty<string>(),
            Category: null,
            samplePublishedAt,
            resource.UpdatedAt,
            SearchVisibility.Public,
            resource.Language,
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
