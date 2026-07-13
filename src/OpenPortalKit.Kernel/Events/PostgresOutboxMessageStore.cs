using System.Data;
using System.Data.Common;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Kernel.Events;

public sealed class PostgresOutboxMessageStore : IOutboxMessageStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresOutboxMessageStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<OutboxMessage> AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_outbox_messages (
                id, event_name, payload_json, idempotency_key, occurred_at, processed_at, attempt_count, last_error, lease_expires_at)
            values (
                @id, @event_name, cast(@payload_json as jsonb), @idempotency_key, @occurred_at, @processed_at,
                @attempt_count, @last_error, @lease_expires_at)
            on conflict (idempotency_key) do nothing
            returning id, event_name, payload_json::text, idempotency_key, occurred_at, processed_at,
                attempt_count, last_error, lease_expires_at
            """;
        AddMessageParameters(command, message);

        OutboxMessage? inserted = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                inserted = ReadMessage(reader);
            }
        }

        if (inserted is not null) return inserted;

        return (await FindByIdempotencyKeyAsync(connection, message.IdempotencyKey, cancellationToken)) ??
            throw new InvalidOperationException("Outbox insert did not return or persist a message.");
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        int maxAttemptCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttemptCount);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, event_name, payload_json::text, idempotency_key, occurred_at, processed_at,
                attempt_count, last_error, lease_expires_at
            from opk_outbox_messages
            where processed_at is null
                and attempt_count < @max_attempt_count
                and (lease_expires_at is null or lease_expires_at <= now())
            order by occurred_at, id
            limit @batch_size
            """;
        command.AddParameter("@max_attempt_count", maxAttemptCount, DbType.Int32);
        command.AddParameter("@batch_size", batchSize, DbType.Int32);

        return await ReadMessagesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        int maxAttemptCount,
        DateTimeOffset leaseExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttemptCount);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with candidates as (
                select id
                from opk_outbox_messages
                where processed_at is null
                    and attempt_count < @max_attempt_count
                    and (lease_expires_at is null or lease_expires_at <= now())
                order by occurred_at, id
                limit @batch_size
                for update skip locked
            )
            update opk_outbox_messages as message
            set lease_expires_at = @lease_expires_at,
                updated_at = now()
            from candidates
            where message.id = candidates.id
            returning message.id, message.event_name, message.payload_json::text, message.idempotency_key,
                message.occurred_at, message.processed_at, message.attempt_count, message.last_error,
                message.lease_expires_at
            """;
        command.AddParameter("@max_attempt_count", maxAttemptCount, DbType.Int32);
        command.AddParameter("@batch_size", batchSize, DbType.Int32);
        command.AddParameter("@lease_expires_at", leaseExpiresAt, DbType.DateTimeOffset);

        return await ReadMessagesAsync(command, cancellationToken);
    }

    public async Task<OutboxMessage?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await FindByIdempotencyKeyAsync(connection, idempotencyKey, cancellationToken);
    }

    public async Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update opk_outbox_messages
            set processed_at = @processed_at,
                last_error = null,
                lease_expires_at = null,
                updated_at = now()
            where id = @id
            """;
        command.AddParameter("@id", messageId, DbType.Guid);
        command.AddParameter("@processed_at", processedAt, DbType.DateTimeOffset);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid messageId,
        string lastError,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastError);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update opk_outbox_messages
            set attempt_count = attempt_count + 1,
                last_error = @last_error,
                lease_expires_at = null,
                updated_at = @attempted_at
            where id = @id
            """;
        command.AddParameter("@id", messageId, DbType.Guid);
        command.AddParameter("@last_error", lastError, DbType.String);
        command.AddParameter("@attempted_at", attemptedAt, DbType.DateTimeOffset);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OutboxMessage?> FindByIdempotencyKeyAsync(
        DbConnection connection,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, event_name, payload_json::text, idempotency_key, occurred_at, processed_at,
                attempt_count, last_error, lease_expires_at
            from opk_outbox_messages
            where idempotency_key = @idempotency_key
            """;
        command.AddParameter("@idempotency_key", idempotencyKey, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMessage(reader) : null;
    }

    private static async Task<IReadOnlyList<OutboxMessage>> ReadMessagesAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var messages = new List<OutboxMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadMessage(reader));
        }

        return messages;
    }

    private static void AddMessageParameters(DbCommand command, OutboxMessage message)
    {
        command.AddParameter("@id", message.Id, DbType.Guid);
        command.AddParameter("@event_name", message.EventName, DbType.String);
        command.AddParameter("@payload_json", message.PayloadJson, DbType.String);
        command.AddParameter("@idempotency_key", message.IdempotencyKey, DbType.String);
        command.AddParameter("@occurred_at", message.OccurredAt, DbType.DateTimeOffset);
        command.AddParameter("@processed_at", message.ProcessedAt, DbType.DateTimeOffset);
        command.AddParameter("@attempt_count", message.AttemptCount, DbType.Int32);
        command.AddParameter("@last_error", message.LastError, DbType.String);
        command.AddParameter("@lease_expires_at", message.LeaseExpiresAt, DbType.DateTimeOffset);
    }

    private static OutboxMessage ReadMessage(DbDataReader reader)
    {
        return new OutboxMessage(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.ReadDateTimeOffset(4),
            reader.IsDBNull(5) ? null : reader.ReadDateTimeOffset(5),
            reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.ReadDateTimeOffset(8));
    }
}
