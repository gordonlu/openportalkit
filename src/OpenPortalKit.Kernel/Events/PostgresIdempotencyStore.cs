using System.Data;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Kernel.Events;

public sealed class PostgresIdempotencyStore : IIdempotencyStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresIdempotencyStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<bool> IsProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists (select 1 from opk_idempotency_keys where idempotency_key = @idempotency_key)";
        command.AddParameter("@idempotency_key", idempotencyKey, DbType.String);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task MarkProcessedAsync(
        string idempotencyKey,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_idempotency_keys (idempotency_key, processed_at)
            values (@idempotency_key, @processed_at)
            on conflict (idempotency_key) do nothing
            """;
        command.AddParameter("@idempotency_key", idempotencyKey, DbType.String);
        command.AddParameter("@processed_at", processedAt, DbType.DateTimeOffset);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
