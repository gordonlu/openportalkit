using System.Data;
using System.Data.Common;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Migration.LegacyContent;

public sealed class PostgresLegacyMigrationBatchStore : ILegacyMigrationBatchStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresLegacyMigrationBatchStore(IOpenPortalKitDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<LegacyMigrationBatch>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " order by staged_at desc, id desc";
        return await ReadAsync(command, cancellationToken);
    }

    public async Task<LegacyMigrationBatch?> FindBySourceBatchAsync(
        string source,
        string importBatch,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where source = @source and import_batch = @import_batch";
        command.AddParameter("@source", source, DbType.String);
        command.AddParameter("@import_batch", importBatch, DbType.String);
        return (await ReadAsync(command, cancellationToken)).SingleOrDefault();
    }

    public async Task<LegacyMigrationBatch?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where id = @id";
        command.AddParameter("@id", id, DbType.Guid);
        return (await ReadAsync(command, cancellationToken)).SingleOrDefault();
    }

    public async Task AddAsync(LegacyMigrationBatch batch, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_legacy_migration_batches (
                id, source, import_batch, as_of_date, schema_version, source_checksum, report_json,
                total_rows, valid_rows, error_count, warning_count, status, staged_by, staged_at)
            values (
                @id, @source, @import_batch, @as_of_date, @schema_version, @source_checksum,
                cast(@report_json as jsonb), @total_rows, @valid_rows, @error_count, @warning_count,
                @status, @staged_by, @staged_at)
            """;
        AddParameters(command, batch);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> MarkRolledBackAsync(
        Guid id,
        Guid actorId,
        DateTimeOffset rolledBackAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update opk_legacy_migration_batches
            set status = 'RolledBack', rolled_back_by = @actor_id, rolled_back_at = @rolled_back_at
            where id = @id and status = 'Staged'
            """;
        command.AddParameter("@id", id, DbType.Guid);
        command.AddParameter("@actor_id", actorId, DbType.Guid);
        command.AddParameter("@rolled_back_at", rolledBackAt, DbType.DateTimeOffset);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private const string SelectBase = """
        select id, source, import_batch, as_of_date, schema_version, source_checksum, report_json::text,
               total_rows, valid_rows, error_count, warning_count, status, staged_by, staged_at,
               rolled_back_by, rolled_back_at
        from opk_legacy_migration_batches
        """;

    private static void AddParameters(DbCommand command, LegacyMigrationBatch batch)
    {
        command.AddParameter("@id", batch.Id, DbType.Guid);
        command.AddParameter("@source", batch.Source, DbType.String);
        command.AddParameter("@import_batch", batch.ImportBatch, DbType.String);
        command.AddParameter("@as_of_date", batch.AsOfDate.ToDateTime(TimeOnly.MinValue), DbType.Date);
        command.AddParameter("@schema_version", batch.SchemaVersion, DbType.String);
        command.AddParameter("@source_checksum", batch.SourceChecksum, DbType.String);
        command.AddParameter("@report_json", batch.ReportJson, DbType.String);
        command.AddParameter("@total_rows", batch.TotalRows, DbType.Int32);
        command.AddParameter("@valid_rows", batch.ValidRows, DbType.Int32);
        command.AddParameter("@error_count", batch.ErrorCount, DbType.Int32);
        command.AddParameter("@warning_count", batch.WarningCount, DbType.Int32);
        command.AddParameter("@status", batch.Status.ToString(), DbType.String);
        command.AddParameter("@staged_by", batch.StagedBy, DbType.Guid);
        command.AddParameter("@staged_at", batch.StagedAt, DbType.DateTimeOffset);
    }

    private static async Task<IReadOnlyList<LegacyMigrationBatch>> ReadAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var results = new List<LegacyMigrationBatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new LegacyMigrationBatch(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                DateOnly.FromDateTime(reader.GetDateTime(3)), reader.GetString(4), reader.GetString(5),
                reader.GetString(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9),
                reader.GetInt32(10), Enum.Parse<LegacyMigrationBatchStatus>(reader.GetString(11), true),
                reader.GetGuid(12), reader.ReadDateTimeOffset(13),
                reader.IsDBNull(14) ? null : reader.GetGuid(14),
                reader.IsDBNull(15) ? null : reader.ReadDateTimeOffset(15)));
        }
        return results;
    }
}
