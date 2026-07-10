using System.Data;
using System.Text.Json;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed class PostgresPublicOutputRevalidationStore : IPublicOutputRevalidationStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresPublicOutputRevalidationStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AddAsync(PublicOutputRevalidationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_public_output_revalidations (
                id, source_event_name, source_idempotency_key, started_at, completed_at,
                invalidated_routes_json, regenerated_outputs_json, succeeded, error)
            values (
                @id, @source_event_name, @source_idempotency_key, @started_at, @completed_at,
                cast(@invalidated_routes_json as jsonb), cast(@regenerated_outputs_json as jsonb), @succeeded, @error)
            on conflict (source_idempotency_key) do nothing
            """;
        command.AddParameter("@id", result.Id, DbType.Guid);
        command.AddParameter("@source_event_name", result.SourceEventName, DbType.String);
        command.AddParameter("@source_idempotency_key", result.SourceIdempotencyKey, DbType.String);
        command.AddParameter("@started_at", result.StartedAt, DbType.DateTimeOffset);
        command.AddParameter("@completed_at", result.CompletedAt, DbType.DateTimeOffset);
        command.AddParameter("@invalidated_routes_json", JsonSerializer.Serialize(result.InvalidatedRoutes), DbType.String);
        command.AddParameter("@regenerated_outputs_json", JsonSerializer.Serialize(result.RegeneratedOutputs), DbType.String);
        command.AddParameter("@succeeded", result.Succeeded, DbType.Boolean);
        command.AddParameter("@error", result.Error, DbType.String);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicOutputRevalidationResult>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " order by started_at asc, id asc";
        return await ReadResultsAsync(command, cancellationToken);
    }

    public async Task<PublicOutputRevalidationResult?> FindByIdempotencyKeyAsync(
        string sourceIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdempotencyKey);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where source_idempotency_key = @source_idempotency_key";
        command.AddParameter("@source_idempotency_key", sourceIdempotencyKey, DbType.String);

        var results = await ReadResultsAsync(command, cancellationToken);
        return results.FirstOrDefault();
    }

    private const string SelectBase = """
        select id, source_event_name, source_idempotency_key, started_at, completed_at,
            invalidated_routes_json::text, regenerated_outputs_json::text, succeeded, error
        from opk_public_output_revalidations
        """;

    private static async Task<IReadOnlyList<PublicOutputRevalidationResult>> ReadResultsAsync(
        System.Data.Common.DbCommand command,
        CancellationToken cancellationToken)
    {
        var results = new List<PublicOutputRevalidationResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PublicOutputRevalidationResult(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.ReadDateTimeOffset(3),
                reader.ReadDateTimeOffset(4),
                JsonSerializer.Deserialize<string[]>(reader.GetString(5)) ?? Array.Empty<string>(),
                JsonSerializer.Deserialize<string[]>(reader.GetString(6)) ?? Array.Empty<string>(),
                reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return results;
    }
}
