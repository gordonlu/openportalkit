using System.Data;
using System.Data.Common;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Kernel.Audit;

public sealed class PostgresAuditLogStore : IAuditLogStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresAuditLogStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<AuditLog> AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_audit_logs (
                id, actor_id, action, target_type, target_id, summary, metadata_json, occurred_at)
            values (
                @id, @actor_id, @action, @target_type, @target_id, @summary,
                case when @metadata_json is null then null else cast(@metadata_json as jsonb) end,
                @occurred_at)
            """;
        AddParameters(command, log);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return log;
    }

    public async Task<IReadOnlyList<AuditLog>> FindByActorAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where actor_id = @actor_id order by occurred_at desc, id desc";
        command.AddParameter("@actor_id", actorId, DbType.Guid);
        return await ReadLogsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where target_type = @target_type and target_id = @target_id order by occurred_at desc, id desc";
        command.AddParameter("@target_type", targetType, DbType.String);
        command.AddParameter("@target_id", targetId, DbType.String);
        return await ReadLogsAsync(command, cancellationToken);
    }

    private const string SelectBase = """
        select id, actor_id, action, target_type, target_id, summary, metadata_json::text, occurred_at
        from opk_audit_logs
        """;

    private static void AddParameters(DbCommand command, AuditLog log)
    {
        command.AddParameter("@id", log.Id, DbType.Guid);
        command.AddParameter("@actor_id", log.ActorId, DbType.Guid);
        command.AddParameter("@action", log.Action, DbType.String);
        command.AddParameter("@target_type", log.TargetType, DbType.String);
        command.AddParameter("@target_id", log.TargetId, DbType.String);
        command.AddParameter("@summary", log.Summary, DbType.String);
        command.AddParameter("@metadata_json", log.MetadataJson, DbType.String);
        command.AddParameter("@occurred_at", log.OccurredAt, DbType.DateTimeOffset);
    }

    private static async Task<IReadOnlyList<AuditLog>> ReadLogsAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var logs = new List<AuditLog>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(new AuditLog(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.ReadDateTimeOffset(7)));
        }

        return logs;
    }
}
