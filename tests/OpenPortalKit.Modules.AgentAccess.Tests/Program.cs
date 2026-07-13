using System.Text.Json;
using OpenPortalKit.Modules.AgentAccess.AgentOutputs;
using OpenPortalKit.Modules.Seo.Revalidation;

var tests = new (string Name, Func<Task> Run)[]
{
    ("markdown snapshot includes citable agent fields", MarkdownSnapshotIncludesCitableAgentFields),
    ("json snapshot preserves policy and citations", JsonSnapshotPreservesPolicyAndCitations),
    ("llms text publishes concise and full discovery", LlmsTextPublishesConciseAndFullDiscovery),
    ("agent manifest exposes public resources and bot policy", AgentManifestExposesPublicResourcesAndBotPolicy),
    ("openapi document describes public read endpoints", OpenApiDocumentDescribesPublicReadEndpoints),
    ("bot policy options normalize allow list", BotPolicyOptionsNormalizeAllowList),
    ("artifact generator creates traceable snapshot artifacts", ArtifactGeneratorCreatesTraceableSnapshotArtifacts),
    ("artifact regenerator stores outputs for revalidation plans", ArtifactRegeneratorStoresOutputsForRevalidationPlans),
    ("publishing artifact factory resolves snapshot content", PublishingArtifactFactoryResolvesSnapshotContent),
    ("publishing artifact factory skips archived snapshot generation", PublishingArtifactFactorySkipsArchivedSnapshotGeneration),
    ("publishing event resolver preserves immutable public content", PublishingEventResolverPreservesImmutablePublicContent),
    ("postgres migration preserves artifact traceability", PostgresMigrationPreservesArtifactTraceability),
    ("postgres store SQL upserts traceable artifacts", PostgresStoreSqlUpsertsTraceableArtifacts),
    ("admin host registers agent access stores", AdminHostRegistersAgentAccessStores)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static Task MarkdownSnapshotIncludesCitableAgentFields()
{
    var markdown = AgentSnapshotGenerator.GenerateMarkdown(CreateDocument());

    Assert.Contains("# Launch Notes", markdown);
    Assert.Contains("Canonical URL: https://portal.example/content/launch-notes", markdown);
    Assert.Contains("## Key Facts", markdown);
    Assert.Contains("## Data Sources", markdown);
    Assert.Contains("AI training: blocked", markdown);
    Assert.Contains("RAG extraction: allowed", markdown);

    return Task.CompletedTask;
}

static Task JsonSnapshotPreservesPolicyAndCitations()
{
    using var json = JsonDocument.Parse(AgentSnapshotGenerator.GenerateJson(CreateDocument()));
    var root = json.RootElement;

    Assert.Equal("Launch Notes", root.GetProperty("title").GetString());
    Assert.Equal("https://portal.example/content/launch-notes", root.GetProperty("canonicalUrl").GetString());
    Assert.False(root.GetProperty("agentVisibilityPolicy").GetProperty("allowAiTraining").GetBoolean(), "Expected AI training to be blocked.");
    Assert.Equal("Sitemap", root.GetProperty("citations")[0].GetProperty("title").GetString());
    Assert.Equal("Related guide", root.GetProperty("relatedContent")[0].GetProperty("title").GetString());

    return Task.CompletedTask;
}

static Task LlmsTextPublishesConciseAndFullDiscovery()
{
    var profile = CreateProfile();
    var document = CreateDocument();
    var concise = LlmsTextGenerator.Generate(profile, new[] { document }, includeFullContent: false);
    var full = LlmsTextGenerator.Generate(profile, new[] { document }, includeFullContent: true);

    Assert.Contains("## Machine-Readable Resources", concise);
    Assert.Contains("OpenAPI: https://portal.example/api/openapi.json", concise);
    Assert.Contains("[Launch Notes](https://portal.example/content/launch-notes)", concise);
    Assert.DoesNotContain("This body is suitable for retrieval", concise);
    Assert.Contains("This body is suitable for retrieval", full);

    return Task.CompletedTask;
}

static Task AgentManifestExposesPublicResourcesAndBotPolicy()
{
    using var json = JsonDocument.Parse(AgentManifestGenerator.GenerateJson(
        CreateProfile(),
        new AgentBotPolicy(true, false, new[] { "OpenPortalKit-Smoke" }, 2),
        new Uri("https://portal.example/api/public/search"),
        new[] { new AgentLink("Sample Catalog", new Uri("https://portal.example/api/public/datasets/sample-catalog")) }));
    var root = json.RootElement;

    Assert.Equal("OpenPortalKit Public Portal", root.GetProperty("siteName").GetString());
    Assert.Equal("https://portal.example/llms.txt", root.GetProperty("llmsText").GetString());
    Assert.False(root.GetProperty("botPolicy").GetProperty("allowTrainingBots").GetBoolean(), "Expected training bots to be blocked.");
    Assert.Equal("Sample Catalog", root.GetProperty("datasetEndpoints")[0].GetProperty("title").GetString());

    return Task.CompletedTask;
}

static Task OpenApiDocumentDescribesPublicReadEndpoints()
{
    using var json = JsonDocument.Parse(AgentOpenApiGenerator.Generate(CreateProfile()));
    var paths = json.RootElement.GetProperty("paths");

    Assert.Equal("3.1.0", json.RootElement.GetProperty("openapi").GetString());
    Assert.True(paths.TryGetProperty("/api/public/content/{slug}.json", out _), "Expected JSON snapshot path.");
    Assert.True(paths.TryGetProperty("/content/{slug}.md", out _), "Expected Markdown snapshot path.");
    Assert.True(paths.TryGetProperty("/pages/{slug}", out _), "Expected public page path.");
    Assert.True(paths.TryGetProperty("/pages/{slug}.md", out _), "Expected public page Markdown path.");
    Assert.True(paths.TryGetProperty("/api/public/pages/{slug}.json", out _), "Expected public page JSON path.");
    var contentGet = paths.GetProperty("/api/public/content").GetProperty("get");
    Assert.Equal(2, contentGet.GetProperty("parameters").GetArrayLength());
    Assert.True(contentGet.GetProperty("responses").TryGetProperty("400", out _), "Expected pagination validation response.");
    Assert.True(paths.GetProperty("/api/public/content/{slug}.json").GetProperty("get")
        .GetProperty("responses").TryGetProperty("304", out _), "Expected conditional GET response.");
    Assert.True(paths.TryGetProperty("/api/public/datasets/{code}/records/{recordKey}", out _), "Expected dataset record path.");
    Assert.True(paths.TryGetProperty("/llms.txt", out _), "Expected llms.txt path.");
    Assert.True(paths.TryGetProperty("/.well-known/agent.json", out _), "Expected agent manifest path.");

    return Task.CompletedTask;
}

static Task BotPolicyOptionsNormalizeAllowList()
{
    var policy = new AgentBotPolicyOptions
    {
        AllowSearchBots = true,
        AllowTrainingBots = false,
        AllowedUserAgents = new[] { " OpenPortalKit-Smoke ", "openportalkit-smoke", "" },
        CrawlDelaySeconds = 3
    }.ToPolicy();

    Assert.Single(policy.AllowedUserAgents);
    Assert.Equal("OpenPortalKit-Smoke", policy.AllowedUserAgents[0]);
    Assert.Equal(3, policy.CrawlDelaySeconds);

    return Task.CompletedTask;
}

static async Task ArtifactGeneratorCreatesTraceableSnapshotArtifacts()
{
    var generatedAt = new DateTimeOffset(2026, 7, 9, 3, 0, 0, TimeSpan.Zero);
    var artifacts = AgentOutputArtifactGenerator.GenerateContentArtifacts(CreateDocument(), generatedAt);
    var json = artifacts.First(artifact => artifact.Path == "/api/public/content/launch-notes.json");

    Assert.Equal(2, artifacts.Count);
    Assert.True(artifacts.Any(artifact => artifact.Path == "/content/launch-notes.md"), "Expected Markdown artifact.");
    Assert.Equal(AgentOutputArtifactGenerator.SchemaVersion, json.SchemaVersion);
    Assert.Equal("content:launch-notes", json.SourceId);
    Assert.Equal(generatedAt, json.GeneratedAt);
    Assert.True(!string.IsNullOrWhiteSpace(json.Checksum), "Expected JSON artifact checksum.");
    Assert.Equal(json.Checksum, AgentOutputArtifactGenerator.ComputeChecksum(json.Body));

    var store = new InMemoryAgentOutputArtifactStore();
    await store.UpsertAsync(json);
    Assert.Equal(json.Checksum, (await store.FindByPathAsync(json.Path))?.Checksum);
}

static async Task ArtifactRegeneratorStoresOutputsForRevalidationPlans()
{
    var store = new InMemoryAgentOutputArtifactStore();
    var document = CreateDocument();
    var generatedAt = new DateTimeOffset(2026, 7, 9, 4, 0, 0, TimeSpan.Zero);
    var regenerator = new AgentOutputArtifactRegenerator(
        store,
        (_, _) => Task.FromResult(AgentOutputArtifactGenerator.GenerateContentArtifacts(document, generatedAt)));
    var plan = new PublicOutputRevalidationPlan(
        "content.published",
        "content:launch-notes:published",
        generatedAt,
        new[] { "/content/launch-notes.md", "/api/public/content/launch-notes.json" },
        RegenerateSitemap: true,
        RegenerateRss: true,
        RegenerateSnapshots: true,
        InvalidateRouteCache: true,
        WarmImportantPages: true,
        SnapshotRoutes: new[] { "/content/launch-notes.md", "/api/public/content/launch-notes.json" },
        RegenerateLlmsText: true);

    var paths = await regenerator.RegenerateAsync(plan);
    var artifacts = await store.ListAsync();

    Assert.Equal(2, paths.Count);
    Assert.Equal(2, artifacts.Count);
    Assert.True(paths.Contains("/content/launch-notes.md"), "Expected Markdown path.");
    Assert.True(paths.Contains("/api/public/content/launch-notes.json"), "Expected JSON path.");
}

static async Task PublishingArtifactFactoryResolvesSnapshotContent()
{
    var resolver = new FixedAgentContentDocumentResolver(CreateDocument());
    var generatedAt = new DateTimeOffset(2026, 7, 10, 5, 0, 0, TimeSpan.Zero);
    var factory = new PublishingAgentOutputArtifactFactory(resolver, () => generatedAt);
    var plan = new PublicOutputRevalidationPlan(
        "content.published",
        "content:launch-notes:published",
        generatedAt,
        new[] { "/content/launch-notes.md", "/api/public/content/launch-notes.json" },
        RegenerateSitemap: true,
        RegenerateRss: true,
        RegenerateSnapshots: true,
        InvalidateRouteCache: true,
        WarmImportantPages: true,
        SnapshotRoutes: new[] { "/content/launch-notes.md", "/api/public/content/launch-notes.json" },
        RegenerateLlmsText: true);

    var artifacts = await factory.CreateArtifactsAsync(plan);

    Assert.Equal(1, resolver.Calls.Count);
    Assert.Equal("launch-notes", resolver.Calls[0]);
    Assert.Equal(2, artifacts.Count);
    Assert.True(artifacts.Any(artifact => artifact.Path == "/content/launch-notes.md"), "Expected Markdown artifact.");
    Assert.True(artifacts.Any(artifact => artifact.Path == "/api/public/content/launch-notes.json"), "Expected JSON artifact.");
    Assert.True(artifacts.All(artifact => artifact.GeneratedAt == generatedAt), "Expected stable generated timestamp.");
}

static async Task PublishingArtifactFactorySkipsArchivedSnapshotGeneration()
{
    var resolver = new FixedAgentContentDocumentResolver(CreateDocument());
    var factory = new PublishingAgentOutputArtifactFactory(resolver);
    var plan = new PublicOutputRevalidationPlan(
        "content.archived",
        "content:launch-notes:archived",
        DateTimeOffset.UtcNow,
        new[] { "/content/launch-notes.md", "/api/public/content/launch-notes.json" },
        RegenerateSitemap: true,
        RegenerateRss: true,
        RegenerateSnapshots: false,
        InvalidateRouteCache: true,
        WarmImportantPages: false,
        SnapshotRoutes: Array.Empty<string>(),
        RegenerateLlmsText: true);

    var artifacts = await factory.CreateArtifactsAsync(plan);

    Assert.Equal(0, artifacts.Count);
    Assert.Equal(0, resolver.Calls.Count);
}

static async Task PublishingEventResolverPreservesImmutablePublicContent()
{
    var generatedAt = new DateTimeOffset(2026, 7, 10, 6, 0, 0, TimeSpan.Zero);
    var payload = JsonSerializer.Serialize(new
    {
        ContentItemId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Slug = "launch-notes",
        Title = "Launch Notes",
        Summary = "Public launch summary.",
        Body = "This body is suitable for retrieval and citation.",
        Source = "Publishing event",
        Tags = new[] { "agent", "publishing" },
        PublishedAt = generatedAt,
        UpdatedAt = generatedAt
    });
    var plan = new PublicOutputRevalidationPlan(
        "ContentPublished",
        "content:launch-notes:published",
        generatedAt,
        new[] { "/content/launch-notes.md" },
        RegenerateSitemap: true,
        RegenerateRss: true,
        RegenerateSnapshots: true,
        InvalidateRouteCache: true,
        WarmImportantPages: true,
        SnapshotRoutes: new[] { "/content/launch-notes.md" },
        RegenerateLlmsText: true,
        SourcePayloadJson: payload);
    var resolver = new PublishingEventAgentContentDocumentResolver(new AgentOutputGenerationOptions
    {
        PublicBaseUrl = "https://portal.example",
        AuthorDisplayName = "OpenPortalKit Editorial"
    });

    var document = await resolver.FindPublishedBySlugAsync(plan, "launch-notes");

    Assert.Equal("content:11111111-1111-1111-1111-111111111111", document?.Id);
    Assert.Equal("Launch Notes", document?.Title);
    Assert.Equal("https://portal.example/content/launch-notes", document?.CanonicalUrl.ToString());
    Assert.Equal("Publishing event", document?.Source);
}

static Task PostgresMigrationPreservesArtifactTraceability()
{
    var sql = File.ReadAllText(Path.Combine(
        "db",
        "postgresql",
        "migrations",
        "0008_agent_output_artifacts.sql"));

    Assert.Contains("create table if not exists opk_agent_output_artifacts", sql);
    Assert.Contains("path text primary key", sql);
    Assert.Contains("source_id text not null", sql);
    Assert.Contains("source_kind text not null", sql);
    Assert.Contains("schema_version text not null", sql);
    Assert.Contains("checksum text not null", sql);
    Assert.Contains("generated_at timestamptz not null", sql);
    Assert.Contains("ix_opk_agent_output_artifacts_source", sql);
    Assert.Contains("ix_opk_agent_output_artifacts_checksum", sql);

    return Task.CompletedTask;
}

static Task PostgresStoreSqlUpsertsTraceableArtifacts()
{
    var sql = string.Join(
        "\n",
        AgentOutputPostgresSql.UpsertArtifact,
        AgentOutputPostgresSql.SelectArtifactByPath,
        AgentOutputPostgresSql.SelectArtifacts);

    Assert.Contains("opk_agent_output_artifacts", sql);
    Assert.Contains("on conflict (path) do update", sql);
    Assert.Contains("source_id", sql);
    Assert.Contains("source_kind", sql);
    Assert.Contains("schema_version", sql);
    Assert.Contains("checksum", sql);
    Assert.Contains("generated_at", sql);
    Assert.Contains("updated_at = now()", sql);

    return Task.CompletedTask;
}

static Task AdminHostRegistersAgentAccessStores()
{
    var apiProgram = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.ApiHost",
        "Program.cs"));
    var program = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.AdminHost",
        "Program.cs"));
    var jobProgram = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.JobHost",
        "Program.cs"));
    var pageModel = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.AdminHost",
        "Pages",
        "AgentAccess",
        "Index.cshtml.cs"));

    Assert.Contains("AgentOutputPostgresStorageOptions", program);
    Assert.Contains("IAgentOutputArtifactStore", program);
    Assert.Contains("PostgresAgentOutputArtifactStore", program);
    Assert.Contains("InMemoryAgentOutputArtifactStore", program);
    Assert.Contains("AgentOutputPostgresStorageOptions", apiProgram);
    Assert.Contains("PostgresAgentOutputArtifactStore", apiProgram);
    Assert.Contains("InMemoryAgentOutputArtifactStore", apiProgram);
    Assert.Contains("IPublicOutputRevalidationStore", program);
    Assert.Contains("IPublicOutputRevalidationExecutor", program);
    Assert.Contains("IPublicOutputRegenerator", program);
    Assert.Contains("PublishingAgentOutputArtifactFactory", program);
    Assert.Contains("PublishingEventAgentContentDocumentResolver", program);
    Assert.Contains("ContentStoreAgentContentDocumentResolver", program);
    Assert.Contains("IAuditLogStore", program);
    Assert.Contains("PostgresOutboxMessageStore", program);
    Assert.Contains("PostgresPublicOutputRevalidationStore", program);
    Assert.Contains("PublishingOutboxWorker", jobProgram);
    Assert.Contains("PostgresIdempotencyStore", jobProgram);
    Assert.Contains("IOptions<AgentBotPolicyOptions>", pageModel);
    Assert.Contains("OnGetAsync", pageModel);
    Assert.DoesNotContain("SeedDevelopmentStateIfEmptyAsync", pageModel);
    Assert.DoesNotContain("UpsertAsync", pageModel);

    return Task.CompletedTask;
}

static AgentContentDocument CreateDocument()
{
    return new AgentContentDocument(
        "content:launch-notes",
        "Article",
        "Launch Notes",
        "launch-notes",
        "Public launch summary.",
        "This body is suitable for retrieval and citation.",
        new Uri("https://portal.example/content/launch-notes"),
        new DateTimeOffset(2026, 7, 9, 1, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 9, 2, 0, 0, TimeSpan.Zero),
        "Editorial",
        "Publishing seed",
        new[] { "agent", "publishing" },
        new[] { "Snapshots are generated for agents.", "Canonical URLs are preserved." },
        new[] { new AgentLink("Related guide", new Uri("https://portal.example/content/guide")) },
        new[] { new AgentLink("Sitemap", new Uri("https://portal.example/sitemap.xml")) },
        AgentVisibilityPolicy.Default,
        "Cite the canonical URL.");
}

static AgentSiteProfile CreateProfile()
{
    return new AgentSiteProfile(
        "OpenPortalKit Public Portal",
        "Public publishing outputs.",
        new Uri("https://portal.example"),
        new[] { new AgentSection("Content", new Uri("https://portal.example/api/public/content"), "Published content.") },
        new[] { new AgentLink("Public API", new Uri("https://portal.example/api/public")) },
        new Uri("https://portal.example/sitemap.xml"),
        new Uri("https://portal.example/rss.xml"),
        new Uri("https://portal.example/api/public"),
        new Uri("https://portal.example/api/openapi.json"),
        new Uri("https://portal.example/llms.txt"),
        new Uri("https://portal.example/llms-full.txt"),
        new Uri("https://portal.example/.well-known/agent.json"),
        "Read-only public usage.",
        "Cite canonical URLs.");
}

static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    public static void True(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool value, string message)
    {
        if (value)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Single<T>(IReadOnlyCollection<T> values)
    {
        if (values.Count != 1)
        {
            throw new InvalidOperationException($"Expected one item, got {values.Count}.");
        }
    }

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected to find '{expected}'.");
        }
    }

    public static void DoesNotContain(string unexpected, string actual)
    {
        if (actual.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Did not expect to find '{unexpected}'.");
        }
    }
}

internal sealed class FixedAgentContentDocumentResolver : IAgentContentDocumentResolver
{
    private readonly AgentContentDocument _document;

    public FixedAgentContentDocumentResolver(AgentContentDocument document)
    {
        _document = document;
    }

    public List<string> Calls { get; } = new();

    public Task<AgentContentDocument?> FindPublishedBySlugAsync(
        PublicOutputRevalidationPlan plan,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Calls.Add(slug);
        return Task.FromResult<AgentContentDocument?>(_document);
    }
}
