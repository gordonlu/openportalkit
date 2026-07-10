using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;

var tests = new (string Name, Func<Task> Run)[]
{
    ("outbox stores only one message per idempotency key", OutboxStoresOneMessagePerIdempotencyKey),
    ("outbox claims messages with an exclusive lease", OutboxClaimsMessagesWithExclusiveLease),
    ("outbox processor handles and marks messages processed", OutboxProcessorHandlesAndMarksMessagesProcessed),
    ("outbox processor retries failed messages", OutboxProcessorRetriesFailedMessages),
    ("postgres migration defines durable publishing delivery", PostgresMigrationDefinesDurablePublishingDelivery),
    ("audit recorder can query by actor and target", AuditRecorderCanQueryByActorAndTarget)
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

static async Task OutboxStoresOneMessagePerIdempotencyKey()
{
    var store = new InMemoryOutboxMessageStore();
    var first = OutboxMessageFactory.FromIntegrationEvent(new TestIntegrationEvent("same-key"));
    var second = OutboxMessageFactory.FromIntegrationEvent(new TestIntegrationEvent("same-key"));

    var storedFirst = await store.AddAsync(first);
    var storedSecond = await store.AddAsync(second);
    var pending = await store.GetPendingAsync(batchSize: 10, maxAttemptCount: 3);

    Assert.Equal(storedFirst.Id, storedSecond.Id);
    Assert.Equal(1, pending.Count);
}

static async Task OutboxProcessorHandlesAndMarksMessagesProcessed()
{
    var store = new InMemoryOutboxMessageStore();
    var idempotency = new InMemoryIdempotencyStore();
    var handler = new CountingOutboxHandler(PublishingEventNames.ContentPublished);
    var processor = new OutboxProcessor(store, idempotency, new[] { handler });
    var message = OutboxMessageFactory.FromIntegrationEvent(new TestIntegrationEvent("content-1"));

    await store.AddAsync(message);
    var result = await processor.ProcessPendingAsync();
    var stored = await store.FindByIdempotencyKeyAsync("content-1");

    Assert.Equal(1, result.ProcessedCount);
    Assert.Equal(1, handler.CallCount);
    Assert.True(stored?.ProcessedAt is not null, "Expected outbox message to be marked processed.");
    Assert.True(await idempotency.IsProcessedAsync("content-1"), "Expected idempotency key to be marked processed.");
}

static async Task OutboxClaimsMessagesWithExclusiveLease()
{
    var store = new InMemoryOutboxMessageStore();
    await store.AddAsync(OutboxMessageFactory.FromIntegrationEvent(new TestIntegrationEvent("lease-1")));

    var leaseUntil = DateTimeOffset.UtcNow.AddMinutes(1);
    var firstClaim = await store.ClaimPendingAsync(10, 3, leaseUntil);
    var secondClaim = await store.ClaimPendingAsync(10, 3, leaseUntil);
    var stored = await store.FindByIdempotencyKeyAsync("lease-1");

    Assert.Equal(1, firstClaim.Count);
    Assert.Equal(0, secondClaim.Count);
    Assert.Equal(leaseUntil, stored?.LeaseExpiresAt);
}

static async Task OutboxProcessorRetriesFailedMessages()
{
    var store = new InMemoryOutboxMessageStore();
    var idempotency = new InMemoryIdempotencyStore();
    var handler = new FailsOnceOutboxHandler(PublishingEventNames.ContentPublished);
    var processor = new OutboxProcessor(store, idempotency, new[] { handler }, new RetryPolicy(MaxAttemptCount: 3));

    await store.AddAsync(OutboxMessageFactory.FromIntegrationEvent(new TestIntegrationEvent("content-2")));

    var firstRun = await processor.ProcessPendingAsync();
    var afterFirstRun = await store.FindByIdempotencyKeyAsync("content-2");
    var secondRun = await processor.ProcessPendingAsync();
    var afterSecondRun = await store.FindByIdempotencyKeyAsync("content-2");

    Assert.Equal(1, firstRun.FailedCount);
    Assert.Equal(1, afterFirstRun?.AttemptCount);
    Assert.Equal(1, secondRun.ProcessedCount);
    Assert.True(afterSecondRun?.ProcessedAt is not null, "Expected retried message to be marked processed.");
}

static async Task AuditRecorderCanQueryByActorAndTarget()
{
    var store = new InMemoryAuditLogStore();
    var recorder = new AuditRecorder(store);
    var actorId = Guid.NewGuid();

    await recorder.RecordAsync(new AuditRecordRequest(
        actorId,
        "ContentPublished",
        "ContentItem",
        "welcome",
        "Published welcome content."));

    var byActor = await store.FindByActorAsync(actorId);
    var byTarget = await store.FindByTargetAsync("ContentItem", "welcome");

    Assert.Equal(1, byActor.Count);
    Assert.Equal(1, byTarget.Count);
    Assert.Equal("ContentPublished", byTarget[0].Action);
}

static Task PostgresMigrationDefinesDurablePublishingDelivery()
{
    var sql = File.ReadAllText(Path.Combine(
        "db",
        "postgresql",
        "migrations",
        "0009_publishing_delivery.sql"));

    Assert.Contains("create table if not exists opk_outbox_messages", sql);
    Assert.Contains("lease_expires_at timestamptz", sql);
    Assert.Contains("create table if not exists opk_idempotency_keys", sql);
    Assert.Contains("create table if not exists opk_public_output_revalidations", sql);
    Assert.Contains("create table if not exists opk_audit_logs", sql);
    Assert.Contains("ix_opk_outbox_messages_pending", sql);

    return Task.CompletedTask;
}

internal sealed record TestIntegrationEvent(string Key)
    : IntegrationEvent(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        PublishingEventNames.ContentPublished,
        Key);

internal sealed class CountingOutboxHandler : IOutboxMessageHandler
{
    public CountingOutboxHandler(string eventName)
    {
        EventName = eventName;
    }

    public string EventName { get; }

    public int CallCount { get; private set; }

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FailsOnceOutboxHandler : IOutboxMessageHandler
{
    private bool _hasFailed;

    public FailsOnceOutboxHandler(string eventName)
    {
        EventName = eventName;
    }

    public string EventName { get; }

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (!_hasFailed)
        {
            _hasFailed = true;
            throw new InvalidOperationException("Transient test failure.");
        }

        return Task.CompletedTask;
    }
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

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected to find '{expected}'.");
        }
    }
}
