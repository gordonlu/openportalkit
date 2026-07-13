using System.Data.Common;
using Npgsql;
using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Persistence;

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
    Console.WriteLine("PASS PostgreSQL migration is repeatable in an isolated schema");
    Console.WriteLine("PASS PostgreSQL outbox lease and retry behavior round trips");
    Console.WriteLine("PASS PostgreSQL audit metadata round trips");
    Console.WriteLine("PASS PostgreSQL idempotency state round trips");
    return 0;
}
finally
{
    await using var drop = new NpgsqlCommand($"drop schema if exists {schema} cascade", administration);
    await drop.ExecuteNonQueryAsync();
}

static async Task ApplyMigrationTwiceAsync(string root, string connectionString)
{
    var sql = await File.ReadAllTextAsync(
        Path.Combine(root, "db", "postgresql", "migrations", "0009_publishing_delivery.sql"));
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    for (var attempt = 0; attempt < 2; attempt++)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
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
