using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Content.ContentItems;
using System.Text.Json;

var tests = new (string Name, Func<Task> Run)[]
{
    ("slug generator normalizes public URLs", SlugGeneratorNormalizesPublicUrls),
    ("publish rejects content without summary", PublishRejectsContentWithoutSummary),
    ("publish creates version audit log and outbox message", PublishCreatesVersionAuditLogAndOutboxMessage),
    ("block templates reject unsupported configuration", BlockTemplatesRejectUnsupportedConfiguration),
    ("block template service versions and audits saved templates", BlockTemplateServiceVersionsAndAuditsSavedTemplates),
    ("block template migration preserves serialized version history", BlockTemplateMigrationPreservesSerializedVersionHistory),
    ("server rendered blocks encode page configuration", ServerRenderedBlocksEncodePageConfiguration),
    ("page service fixes template version and audits publication", PageServiceFixesTemplateVersionAndAuditsPublication),
    ("public query hides drafts archived and expired content", PublicQueryHidesDraftsArchivedAndExpiredContent),
    ("public query resolves published detail by slug", PublicQueryResolvesPublishedDetailBySlug)
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

static Task SlugGeneratorNormalizesPublicUrls()
{
    Assert.Equal("hello-openportalkit-2026", SlugGenerator.Generate(" Hello, OpenPortalKit 2026! "));
    Assert.Equal("item", SlugGenerator.Generate(" -- "));
    return Task.CompletedTask;
}

static async Task PublishRejectsContentWithoutSummary()
{
    var service = CreateService(out _, out _);
    var item = CreateDraft(summary: "");

    var result = await service.PublishAsync(item, new PublishContentRequest(Guid.NewGuid(), VersionNumber: 1));

    Assert.False(result.Succeeded, "Expected publishing to fail.");
    Assert.Equal(1, result.Errors.Count);
    Assert.Equal("Published content must have a summary.", result.Errors[0]);
}

static async Task PublishCreatesVersionAuditLogAndOutboxMessage()
{
    var service = CreateService(out var auditStore, out var outboxStore);
    var actorId = Guid.NewGuid();
    var publishedAt = new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero);
    var item = CreateDraft(slug: "");

    var result = await service.PublishAsync(
        item,
        new PublishContentRequest(actorId, VersionNumber: 2, PublishedAt: publishedAt));

    var auditLogs = await auditStore.FindByTargetAsync("ContentItem", item.Id.ToString("D"));
    var pending = await outboxStore.GetPendingAsync(batchSize: 10, maxAttemptCount: 3);

    Assert.True(result.Succeeded, "Expected publishing to succeed.");
    Assert.Equal(ContentPublicationStatus.Published, result.Item?.Status);
    Assert.Equal("launch-notes", result.Item?.Slug);
    Assert.Equal(2, result.Version?.VersionNumber);
    Assert.Equal("ContentPublished", auditLogs[0].Action);
    Assert.Equal(1, pending.Count);
    Assert.Equal("ContentPublished", pending[0].EventName);
    using var payload = JsonDocument.Parse(pending[0].PayloadJson);
    Assert.Equal("Launch Notes", payload.RootElement.GetProperty("Title").GetString());
    Assert.Equal("Body content.", payload.RootElement.GetProperty("Body").GetString());
    Assert.Equal(publishedAt, payload.RootElement.GetProperty("PublishedAt").GetDateTimeOffset());
}

static Task BlockTemplatesRejectUnsupportedConfiguration()
{
    var catalog = new PredefinedBlockCatalog();
    var template = CreateTemplate(new BlockInstance(
        Guid.NewGuid(),
        "hero",
        "block.hero.v1",
        0,
        """{"headline":"A safe headline","customCss":"body { display: none; }"}"""));

    var result = PageTemplateValidator.Validate(template, catalog);

    Assert.False(result.IsValid, "Expected unsupported block configuration to be rejected.");
    Assert.Contains("does not allow configuration field 'customCss'", result.Errors);
    return Task.CompletedTask;
}

static async Task BlockTemplateServiceVersionsAndAuditsSavedTemplates()
{
    var auditStore = new InMemoryAuditLogStore();
    var actorId = Guid.NewGuid();
    var template = CreateTemplate(new BlockInstance(
        Guid.NewGuid(),
        "rich-text",
        "block.rich-text.v1",
        0,
        """{"body":"<p>Trusted editorial content.</p>"}""")) with
    {
        Code = "Corporate Homepage!!"
    };
    var templateStore = new InMemoryPageTemplateStore();
    var service = new PageTemplateService(
        templateStore,
        new PredefinedBlockCatalog(),
        new AuditRecorder(auditStore),
        () => new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero));

    var result = await service.SaveAsync(template, actorId);
    var update = await service.SaveAsync(result.Template! with { Name = "Corporate homepage 2026" }, actorId);
    var audits = await auditStore.FindByTargetAsync("PageTemplate", template.Id.ToString("D"));
    var versions = await templateStore.ListVersionsAsync(template.Id);

    Assert.True(result.Succeeded, "Expected valid template to be saved.");
    Assert.Equal("corporate-homepage", result.Template?.Code);
    Assert.Equal(1, result.Template?.Version);
    Assert.Equal(2, update.Template?.Version);
    Assert.Equal(2, audits.Count);
    Assert.Equal("block-template.updated", audits[0].Action);
    Assert.Equal(2, versions.Count);
    Assert.Equal(2, versions[0].Version);
    Assert.Equal(1, versions[1].Version);
}

static Task BlockTemplateMigrationPreservesSerializedVersionHistory()
{
    var sql = File.ReadAllText(Path.Combine(
        "db",
        "postgresql",
        "migrations",
        "0010_block_templates.sql"));

    Assert.Contains("create table if not exists opk_page_templates", sql);
    Assert.Contains("blocks_json jsonb not null", sql);
    Assert.Contains("create table if not exists opk_page_template_versions", sql);
    Assert.Contains("snapshot_json jsonb not null", sql);
    Assert.Contains("primary key (template_id, version)", sql);

    var pageSql = File.ReadAllText(Path.Combine(
        "db",
        "postgresql",
        "migrations",
        "0011_portal_pages.sql"));
    Assert.Contains("create table if not exists opk_portal_pages", pageSql);
    Assert.Contains("template_version integer not null", pageSql);
    Assert.Contains("uq_opk_portal_pages_site_slug", pageSql);

    var adminProgram = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.AdminHost",
        "Program.cs"));
    Assert.Contains("PredefinedBlockCatalog", adminProgram);
    Assert.Contains("PostgresPageTemplateStore", adminProgram);
    Assert.Contains("InMemoryPageTemplateStore", adminProgram);
    Assert.Contains("PostgresPageStore", adminProgram);
    Assert.Contains("InMemoryPageStore", adminProgram);
    return Task.CompletedTask;
}

static Task ServerRenderedBlocksEncodePageConfiguration()
{
    var page = new PortalPage(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        1,
        "Safe page",
        "safe-page",
        "A safe server-rendered page.",
        PortalPageStatus.Published,
        new[]
        {
            new BlockInstance(Guid.NewGuid(), "hero", "block.hero.v1", 0,
                """{"headline":"<script>alert(1)</script>","summary":"A safe summary."}"""),
            new BlockInstance(Guid.NewGuid(), "rich-text", "block.rich-text.v1", 1,
                """{"body":"<img src=x onerror=alert(1)>\nSecond paragraph."}""")
        },
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    var html = new ServerRenderedBlockPageRenderer().RenderBody(page);

    Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
    Assert.False(html.Contains("<script>", StringComparison.Ordinal), "Expected script content to be encoded.");
    Assert.Contains("Second paragraph.", html);
    return Task.CompletedTask;
}

static async Task PageServiceFixesTemplateVersionAndAuditsPublication()
{
    var actorId = Guid.NewGuid();
    var siteId = Guid.NewGuid();
    var now = new DateTimeOffset(2026, 7, 10, 11, 0, 0, TimeSpan.Zero);
    var auditStore = new InMemoryAuditLogStore();
    var templateStore = new InMemoryPageTemplateStore();
    var catalog = new PredefinedBlockCatalog();
    var templateService = new PageTemplateService(
        templateStore,
        catalog,
        new AuditRecorder(auditStore),
        () => now);
    var template = CreateTemplate(new BlockInstance(
        Guid.NewGuid(),
        "hero",
        "block.hero.v1",
        0,
        """{"headline":"A page headline"}""")) with { Status = PageTemplateStatus.Published };
    var savedTemplate = (await templateService.SaveAsync(template, actorId)).Template!;
    var pageStore = new InMemoryPageStore();
    var outboxStore = new InMemoryOutboxMessageStore();
    var pageService = new PortalPageService(
        templateStore,
        pageStore,
        new AuditRecorder(auditStore),
        outboxStore,
        () => now);

    var created = await pageService.CreateFromTemplateAsync(new CreatePageFromTemplateRequest(
        siteId,
        savedTemplate.Code,
        "Portal overview",
        "Portal Overview",
        "A public portal overview.",
        actorId));
    var published = await pageService.PublishAsync(siteId, "portal-overview", actorId);
    var publicPage = await new PublicPageQueryService(pageStore)
        .FindPublishedBySlugAsync(siteId, "portal-overview", now);
    var auditLogs = await auditStore.FindByTargetAsync("PortalPage", created.Page!.Id.ToString("D"));
    var outboxMessages = await outboxStore.GetPendingAsync(10, 3);

    Assert.True(created.Succeeded, "Expected page creation from a published template.");
    Assert.Equal(savedTemplate.Version, created.Page?.TemplateVersion);
    Assert.Equal(PortalPageStatus.Draft, created.Page?.Status);
    Assert.True(created.Page?.Blocks[0].Id != savedTemplate.Blocks[0].Id, "Expected page block IDs to be independent of the template.");
    Assert.Equal(PortalPageStatus.Published, published.Page?.Status);
    Assert.Equal("portal-overview", publicPage?.Slug);
    Assert.Equal(2, auditLogs.Count);
    Assert.Equal("portal-page.published", auditLogs[0].Action);
    Assert.Equal(1, outboxMessages.Count);
    Assert.Equal("PortalPagePublished", outboxMessages[0].EventName);
}

static async Task PublicQueryHidesDraftsArchivedAndExpiredContent()
{
    var store = new InMemoryContentItemStore();
    var siteId = Guid.NewGuid();
    var now = new DateTimeOffset(2026, 7, 6, 9, 30, 0, TimeSpan.Zero);

    await store.AddAsync(CreateContent(siteId, "Published", "published", ContentPublicationStatus.Published, now.AddMinutes(-5)));
    await store.AddAsync(CreateContent(siteId, "Draft", "draft", ContentPublicationStatus.Draft, null));
    await store.AddAsync(CreateContent(siteId, "Archived", "archived", ContentPublicationStatus.Archived, now.AddMinutes(-5)));
    await store.AddAsync(CreateContent(siteId, "Expired", "expired", ContentPublicationStatus.Published, now.AddDays(-2), expiresAt: now.AddDays(-1)));
    await store.AddAsync(CreateContent(siteId, "Future", "future", ContentPublicationStatus.Published, now.AddHours(1)));

    var query = new PublicContentQueryService(store);
    var visible = await query.ListPublishedAsync(new ContentListQuery(SiteId: siteId), now);

    Assert.Equal(1, visible.Count);
    Assert.Equal("published", visible[0].Slug);
}

static async Task PublicQueryResolvesPublishedDetailBySlug()
{
    var store = new InMemoryContentItemStore();
    var siteId = Guid.NewGuid();
    var now = new DateTimeOffset(2026, 7, 6, 9, 45, 0, TimeSpan.Zero);

    await store.AddAsync(CreateContent(siteId, "Quarterly Update", "quarterly-update", ContentPublicationStatus.Published, now.AddMinutes(-1)));

    var query = new PublicContentQueryService(store);
    var detail = await query.FindPublishedBySlugAsync(siteId, "Quarterly Update", now);

    Assert.Equal("Quarterly Update", detail?.Title);
    Assert.Equal("Body content.", detail?.Body);
}

static ContentPublishingService CreateService(
    out InMemoryAuditLogStore auditStore,
    out InMemoryOutboxMessageStore outboxStore)
{
    auditStore = new InMemoryAuditLogStore();
    outboxStore = new InMemoryOutboxMessageStore();
    return new ContentPublishingService(new AuditRecorder(auditStore), outboxStore);
}

static ContentItem CreateDraft(string? summary = null, string slug = "launch-notes")
{
    var now = DateTimeOffset.UtcNow;
    var actorId = Guid.NewGuid();

    return new ContentItem(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Launch Notes",
        slug,
        summary ?? "A short summary for public readers and agents.",
        "Body content.",
        CoverAssetId: null,
        ContentPublicationStatus.Draft,
        CategoryId: null,
        Tags: Array.Empty<string>(),
        AuthorId: actorId,
        Source: "editorial",
        PublishedAt: null,
        ScheduledAt: null,
        ExpiresAt: null,
        CreatedBy: actorId,
        UpdatedBy: actorId,
        CreatedAt: now,
        UpdatedAt: now);
}

static PageTemplate CreateTemplate(BlockInstance block)
{
    return new PageTemplate(
        Guid.NewGuid(),
        "corporate-homepage",
        "Corporate homepage",
        "A reusable corporate landing page template.",
        PageTemplateStatus.Draft,
        1,
        new[] { block },
        Guid.Empty,
        Guid.Empty,
        default,
        default);
}

static ContentItem CreateContent(
    Guid siteId,
    string title,
    string slug,
    ContentPublicationStatus status,
    DateTimeOffset? publishedAt,
    DateTimeOffset? expiresAt = null)
{
    var now = new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero);
    var actorId = Guid.NewGuid();

    return new ContentItem(
        Guid.NewGuid(),
        siteId,
        Guid.NewGuid(),
        title,
        slug,
        $"Summary for {title}.",
        "Body content.",
        CoverAssetId: null,
        status,
        CategoryId: null,
        Tags: new[] { "updates" },
        AuthorId: actorId,
        Source: "editorial",
        PublishedAt: publishedAt,
        ScheduledAt: null,
        ExpiresAt: expiresAt,
        CreatedBy: actorId,
        UpdatedBy: actorId,
        CreatedAt: now,
        UpdatedAt: now);
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Contains(string expected, IReadOnlyList<string> values)
    {
        if (!values.Any(value => value.Contains(expected, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Expected to find '{expected}'.");
        }
    }

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected to find '{expected}'.");
        }
    }
}
