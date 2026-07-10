using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Modules.Content.ContentItems;
using System.Text.Json;

var tests = new (string Name, Func<Task> Run)[]
{
    ("slug generator normalizes public URLs", SlugGeneratorNormalizesPublicUrls),
    ("publish rejects content without summary", PublishRejectsContentWithoutSummary),
    ("publish creates version audit log and outbox message", PublishCreatesVersionAuditLogAndOutboxMessage),
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
}
