using System.Data.Common;
using Npgsql;
using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Persistence;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Content.ContentItems;
using OpenPortalKit.Modules.Data.Datasets;
using OpenPortalKit.Modules.Workflow.Publishing;

var connectionString = Environment.GetEnvironmentVariable("OPK_POSTGRES_INTEGRATION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("OPK_POSTGRES_INTEGRATION is required for PostgreSQL integration tests.");
    return 2;
}

var root = FindRepositoryRoot();
var schema = "opk_test_" + Guid.NewGuid().ToString("N");
var baseBuilder = new NpgsqlConnectionStringBuilder(connectionString)
{
    Pooling = false,
    Timeout = 10,
    CommandTimeout = 30
};
var scopedBuilder = new NpgsqlConnectionStringBuilder(baseBuilder.ConnectionString)
{
    SearchPath = schema
};

await using var administration = new NpgsqlConnection(baseBuilder.ConnectionString);
await administration.OpenAsync();
await using (var create = new NpgsqlCommand($"create schema {schema}", administration))
    await create.ExecuteNonQueryAsync();

try
{
    await ApplyMigrationTwiceAsync(root, scopedBuilder.ConnectionString);
    DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
    var connectionFactory = new PostgresOpenPortalKitDbConnectionFactory(new PostgresPersistenceOptions
    {
        Enabled = true,
        ProviderInvariantName = "Npgsql",
        ConnectionString = scopedBuilder.ConnectionString
    });

    await OutboxLeaseAndIdempotencyRoundTrip(connectionFactory);
    await AuditRoundTrip(connectionFactory);
    await IdempotencyRoundTrip(connectionFactory);
    await ContentInventoryRoundTrip(connectionFactory);
    await StructuredDataRoundTrip(connectionFactory);
    await PortalPageConcurrencyRoundTrip(connectionFactory);
    await PublicPortalPagePaginationRoundTrip(connectionFactory);
    await PublishingWorkflowRoundTrip(connectionFactory);
    Console.WriteLine("PASS PostgreSQL migration is repeatable in an isolated schema");
    Console.WriteLine("PASS PostgreSQL outbox lease and retry behavior round trips");
    Console.WriteLine("PASS PostgreSQL audit metadata round trips");
    Console.WriteLine("PASS PostgreSQL idempotency state round trips");
    Console.WriteLine("PASS PostgreSQL content inventory filters and traceability round trip");
    Console.WriteLine("PASS PostgreSQL structured data catalog and record traceability round trip");
    Console.WriteLine("PASS PostgreSQL portal page optimistic concurrency rejects stale writes");
    Console.WriteLine("PASS PostgreSQL public portal page pagination follows visibility and publication order");
    Console.WriteLine("PASS PostgreSQL workflow review evidence, scheduling, and concurrency round trip");
    return 0;
}
finally
{
    await using var drop = new NpgsqlCommand($"drop schema if exists {schema} cascade", administration);
    await drop.ExecuteNonQueryAsync();
}

static async Task ApplyMigrationTwiceAsync(string root, string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    foreach (var migration in new[]
    {
        "0009_publishing_delivery.sql",
        "0010_block_templates.sql",
        "0011_portal_pages.sql",
        "0012_portal_page_versions.sql",
        "0016_content_items.sql",
        "0017_publishing_workflow.sql",
        "0018_structured_data.sql"
    })
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(root, "db", "postgresql", "migrations", migration));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

static async Task PublishingWorkflowRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var workflowStore = new PostgresPublishingWorkflowItemStore(connectionFactory);
    var approvalStore = new PostgresApprovalRecordStore(connectionFactory);
    var auditStore = new PostgresAuditLogStore(connectionFactory);
    var service = new PublishingWorkflowService(
        new AuditRecorder(auditStore), approvalStore, workflowStore);
    var actorId = Guid.NewGuid();
    var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    var draft = PublishingWorkflowItem.CreateDraft(
        "ContentItem", "workflow-" + Guid.NewGuid().ToString("N"), actorId, now);
    await workflowStore.AddAsync(draft);

    var review = (await service.TransitionAsync(draft, new WorkflowTransitionRequest(
        WorkflowAction.SubmitForReview, actorId, OccurredAt: now.AddMinutes(1)))).Item!;
    var approved = await service.TransitionAsync(review, new WorkflowTransitionRequest(
        WorkflowAction.Approve,
        actorId,
        Comment: "PostgreSQL approval evidence.",
        OccurredAt: now.AddMinutes(2)));
    var stale = await service.TransitionAsync(review, new WorkflowTransitionRequest(
        WorkflowAction.Reject,
        actorId,
        Comment: "This stale decision must not persist.",
        OccurredAt: now.AddMinutes(3)));
    var scheduledAt = now.AddHours(2);
    var scheduled = await service.TransitionAsync(approved.Item!, new WorkflowTransitionRequest(
        WorkflowAction.SchedulePublish,
        actorId,
        ScheduledAt: scheduledAt,
        OccurredAt: now.AddMinutes(4),
        Readiness: new WorkflowPublicationReadiness(true, true, true)));

    var stored = await workflowStore.FindByTargetAsync(draft.TargetType, draft.TargetId);
    var notDue = await workflowStore.ListScheduledDueAsync(scheduledAt.AddSeconds(-1), 10);
    var due = await workflowStore.ListScheduledDueAsync(scheduledAt, 10);
    var approvals = await approvalStore.FindByTargetAsync(draft.TargetType, draft.TargetId);
    var audits = await auditStore.FindByTargetAsync(draft.TargetType, draft.TargetId);

    Assert.True(approved.Succeeded, "Current PostgreSQL approval was rejected.");
    Assert.False(stale.Succeeded, "Stale PostgreSQL review decision was persisted.");
    Assert.True(scheduled.Succeeded, "Approved PostgreSQL workflow item was not scheduled.");
    Assert.Equal(scheduledAt, stored?.ScheduledAt, "Scheduled publication time was not persisted.");
    Assert.Equal(0, notDue.Count, "Scheduled item became due too early.");
    Assert.Equal(1, due.Count, "Scheduled item was not returned when due.");
    Assert.Equal(1, approvals.Count, "Stale decision created approval evidence.");
    Assert.Equal("PostgreSQL approval evidence.", approvals[0].Comment,
        "Approval comment was not preserved.");
    Assert.Equal(3, audits.Count, "Workflow transitions did not create the expected audit evidence.");
}

static async Task PortalPageConcurrencyRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var actorId = Guid.NewGuid();
    var now = DateTimeOffset.UtcNow;
    var template = new PageTemplate(
        Guid.NewGuid(), "integration-page", "Integration page", "Integration template.",
        PageTemplateStatus.Published, 1,
        new[] { new BlockInstance(Guid.NewGuid(), "hero", "block.hero.v1", 0, """{"headline":"Integration"}""") },
        actorId, actorId, now, now);
    await new PostgresPageTemplateStore(connectionFactory).SaveAsync(template);
    var store = new PostgresPageStore(connectionFactory);
    var page = new PortalPage(
        Guid.NewGuid(), Guid.NewGuid(), template.Id, template.Version, "Concurrency page", "concurrency-page",
        "A page used to verify optimistic concurrency.", PortalPageStatus.Draft,
        template.Blocks, actorId, actorId, now, now, null);
    await store.UpsertAsync(page);

    var updated = page with { Title = "Accepted update", Revision = 2, UpdatedAt = now.AddMinutes(1) };
    var accepted = await store.TryUpdateAsync(updated, expectedRevision: 1);
    var stale = await store.TryUpdateAsync(
        updated with { Title = "Stale update", Revision = 2, UpdatedAt = now.AddMinutes(2) },
        expectedRevision: 1);
    var stored = await store.FindBySlugAsync(page.SiteId, page.Slug);
    var versions = await store.ListVersionsAsync(page.Id);

    Assert.True(accepted, "Current portal page revision was rejected.");
    Assert.False(stale, "Stale portal page revision overwrote a newer edit.");
    Assert.Equal("Accepted update", stored?.Title, "Stale portal page content reached storage.");
    Assert.Equal(2, versions.Count, "Rejected page update created a version snapshot.");
}

static async Task PublicPortalPagePaginationRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var actorId = Guid.NewGuid();
    var siteId = Guid.NewGuid();
    var now = DateTimeOffset.UtcNow;
    var template = new PageTemplate(
        Guid.NewGuid(), "public-listing", "Public listing", "Public listing integration template.",
        PageTemplateStatus.Published, 1, Array.Empty<BlockInstance>(), actorId, actorId, now, now);
    await new PostgresPageTemplateStore(connectionFactory).SaveAsync(template);
    var store = new PostgresPageStore(connectionFactory);

    PortalPage CreatePage(string title, PortalPageStatus status, DateTimeOffset? publishedAt) => new(
        Guid.NewGuid(), siteId, template.Id, template.Version, title, SlugGenerator.Generate(title),
        title + " summary.", status, template.Blocks, actorId, actorId, now, now, publishedAt);

    await store.UpsertAsync(CreatePage("Draft", PortalPageStatus.Draft, null));
    await store.UpsertAsync(CreatePage("Older visible", PortalPageStatus.Published, now.AddMinutes(-2)));
    await store.UpsertAsync(CreatePage("Newest visible", PortalPageStatus.Published, now.AddMinutes(-1)));
    await store.UpsertAsync(CreatePage("Future", PortalPageStatus.Published, now.AddMinutes(1)));

    var pages = await store.ListPublishedPageAsync(siteId, now, skip: 1, take: 1);

    Assert.Equal(1, pages.Count, "PostgreSQL public page pagination returned an incorrect page size.");
    Assert.Equal("Older visible", pages[0].Title,
        "PostgreSQL public page pagination ran before visibility filtering or used unstable ordering.");
}

static async Task ContentInventoryRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var store = new PostgresContentItemStore(connectionFactory);
    var siteId = Guid.NewGuid();
    var typeId = Guid.NewGuid();
    var authorId = Guid.NewGuid();
    var now = DateTimeOffset.UtcNow;
    var draft = new ContentItem(
        Guid.NewGuid(), siteId, typeId, "PostgreSQL authoring guide", "postgresql-authoring-guide",
        "A durable content draft.", "Draft body.", null, ContentPublicationStatus.Draft, null,
        new[] { "postgresql", "authoring" }, authorId, "integration-import", null, null, null,
        authorId, authorId, now.AddMinutes(-2), now.AddMinutes(-1));
    var published = draft with
    {
        Id = Guid.NewGuid(),
        Title = "Published operations guide",
        Slug = "published-operations-guide",
        Status = ContentPublicationStatus.Published,
        PublishedAt = now.AddMinutes(-3),
        UpdatedAt = now
    };

    await store.AddAsync(draft);
    await store.AddAsync(published);
    await store.AddAsync(draft with { Summary = "Updated durable content draft.", UpdatedAt = now.AddMinutes(1) });

    var byId = await store.FindByIdAsync(draft.Id);
    var bySlug = await store.FindBySlugAsync(siteId, published.Slug);
    var adminPage = await store.ListAdminAsync(new AdminContentListQuery(
        siteId, "postgresql", ContentPublicationStatus.Draft, typeId, authorId, 0, 10));
    var tagged = await store.ListAsync(new ContentListQuery(siteId, typeId, Tag: "authoring", Take: 10));
    var publicPage = await store.ListPublishedAsync(new ContentListQuery(siteId, Take: 1), now);
    var versions = await store.ListVersionsAsync(draft.Id);

    Assert.Equal("Updated durable content draft.", byId?.Summary,
        "Content upsert did not preserve the latest authored fields.");
    Assert.Equal(published.Id, bySlug?.Id, "Content slug lookup failed.");
    Assert.Equal(1, adminPage.TotalCount, "Admin content filters returned an incorrect total.");
    Assert.Equal(draft.Id, adminPage.Items[0].Id, "Admin content filters returned the wrong item.");
    Assert.Equal("integration-import", adminPage.Items[0].Source, "Content source was not preserved.");
    Assert.Equal(2, tagged.Count, "JSON tag filtering did not return both matching items.");
    Assert.Equal(published.Id, publicPage[0].Id, "Public pagination was applied before visibility filtering.");
    Assert.Equal(2, versions.Count, "Content revision history did not retain both writes.");
    Assert.Equal("Updated durable content draft.", versions[0].Snapshot.Summary,
        "Latest content revision did not preserve the full snapshot.");
    Assert.Equal("A durable content draft.", versions[1].Snapshot.Summary,
        "Earlier content revision was mutated by a later write.");
}

static async Task StructuredDataRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var dataSetStore = new PostgresDataSetStore(connectionFactory);
    var recordStore = new PostgresDataRecordStore(connectionFactory);
    var importService = new DataImportService(dataSetStore, recordStore);
    var siteId = Guid.NewGuid();
    var now = DateTimeOffset.UtcNow;
    var publicDataSet = new DataSet(
        Guid.NewGuid(), siteId, "public-operations", "Public operations", "Traceable public operations data.",
        true, now, now);
    var privateDataSet = new DataSet(
        Guid.NewGuid(), siteId, "private-operations", "Private operations", "Internal operations data.",
        false, now, now);
    var schema = new DataSchemaVersion(
        Guid.NewGuid(), publicDataSet.Id, 1, """{"type":"object","properties":{"value":{"type":"number"}}}""",
        DataChecksum.FromJson("""{"type":"object","properties":{"value":{"type":"number"}}}"""), now);

    await dataSetStore.AddDataSetAsync(publicDataSet);
    await dataSetStore.AddDataSetAsync(privateDataSet);
    await dataSetStore.AddSchemaVersionAsync(schema);
    var actorId = Guid.NewGuid();
    var imported = await importService.ImportAsync(new DataImportRequest(
        publicDataSet.Id, schema.Id, "postgres-integration", new DateOnly(2026, 7, 14), actorId,
        new[] { new DataImportRow("record-1", """{"value":42}""") }, ImportedAt: now));

    var query = new PublicDataSetQueryService(dataSetStore, recordStore);
    var catalog = await query.ListPublicAsync(siteId);
    var detail = await query.FindByCodeAsync(siteId, publicDataSet.Code);
    var storedSchema = await query.FindSchemaByCodeAsync(siteId, publicDataSet.Code);

    Assert.True(imported.Succeeded, "PostgreSQL structured-data import failed.");
    Assert.Equal(1, catalog.Count, "PostgreSQL public catalog leaked a private dataset.");
    Assert.Equal(publicDataSet.Code, catalog[0].Code, "PostgreSQL public catalog returned the wrong dataset.");
    Assert.Equal(1, detail?.Records.Count ?? 0, "PostgreSQL public detail omitted an imported record.");
    Assert.Equal(imported.Batch.Id, detail!.Records[0].SourceBatchId,
        "PostgreSQL structured record did not preserve its source batch.");
    Assert.Equal(new DateOnly(2026, 7, 14), detail.Records[0].AsOfDate,
        "PostgreSQL structured record did not preserve its as-of date.");
    Assert.Equal(schema.Id, detail.Records[0].SchemaVersionId,
        "PostgreSQL structured record did not preserve its schema version.");
    Assert.Equal(schema.Checksum, storedSchema?.Checksum,
        "PostgreSQL structured schema did not preserve its checksum.");
}

static async Task OutboxLeaseAndIdempotencyRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var store = new PostgresOutboxMessageStore(connectionFactory);
    var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
    var message = new OutboxMessage(
        Guid.NewGuid(),
        "content.published",
        "{\"contentId\":\"integration\"}",
        "integration:" + Guid.NewGuid().ToString("N"),
        occurredAt,
        null,
        0,
        null);

    var first = await store.AddAsync(message);
    var duplicate = await store.AddAsync(message with { Id = Guid.NewGuid() });
    Assert.Equal(first.Id, duplicate.Id, "Duplicate idempotency key created another outbox row.");

    var leaseExpiry = DateTimeOffset.UtcNow.AddMinutes(1);
    var firstClaim = await store.ClaimPendingAsync(10, 5, leaseExpiry);
    Assert.Equal(1, firstClaim.Count, "Pending outbox message was not claimed.");
    var competingClaim = await store.ClaimPendingAsync(10, 5, leaseExpiry.AddMinutes(1));
    Assert.Equal(0, competingClaim.Count, "Leased outbox message was claimed twice.");

    await store.MarkFailedAsync(first.Id, "integration failure", DateTimeOffset.UtcNow);
    var retryClaim = await store.ClaimPendingAsync(10, 5, DateTimeOffset.UtcNow.AddMinutes(1));
    Assert.Equal(1, retryClaim.Count, "Failed outbox message was not released for retry.");
    Assert.Equal(1, retryClaim[0].AttemptCount, "Retry attempt count was not persisted.");

    await store.MarkProcessedAsync(first.Id, DateTimeOffset.UtcNow);
    Assert.Equal(0, (await store.GetPendingAsync(10, 5)).Count, "Processed message remained pending.");
}

static async Task AuditRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var store = new PostgresAuditLogStore(connectionFactory);
    var actor = Guid.NewGuid();
    var targetId = Guid.NewGuid().ToString("N");
    var recorder = new AuditRecorder(store);
    await recorder.RecordAsync(new AuditRecordRequest(
        actor,
        "integration.changed",
        "IntegrationTarget",
        targetId,
        "Integration audit record.",
        "{\"traceId\":\"integration-trace\"}"));

    var byActor = await store.FindByActorAsync(actor);
    var byTarget = await store.FindByTargetAsync("IntegrationTarget", targetId);
    Assert.Equal(1, byActor.Count, "Audit lookup by actor failed.");
    Assert.Equal(1, byTarget.Count, "Audit lookup by target failed.");
    Assert.True(byTarget[0].MetadataJson?.Contains("integration-trace", StringComparison.Ordinal) == true,
        "Audit JSON metadata was not preserved.");
}

static async Task IdempotencyRoundTrip(IOpenPortalKitDbConnectionFactory connectionFactory)
{
    var store = new PostgresIdempotencyStore(connectionFactory);
    var key = "integration:" + Guid.NewGuid().ToString("N");
    Assert.False(await store.IsProcessedAsync(key), "New idempotency key was already processed.");
    await store.MarkProcessedAsync(key, DateTimeOffset.UtcNow);
    await store.MarkProcessedAsync(key, DateTimeOffset.UtcNow.AddSeconds(1));
    Assert.True(await store.IsProcessedAsync(key), "Processed idempotency key was not persisted.");
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "OpenPortalKit.sln"))) return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Repository root was not found.");
}

file static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
    }
}
