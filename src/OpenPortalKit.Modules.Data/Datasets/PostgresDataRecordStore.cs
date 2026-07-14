using System.Data;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Data.Datasets;

public sealed class PostgresDataRecordStore : IDataRecordStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresDataRecordStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<DataRecord?> FindByKeyAsync(
        Guid dataSetId,
        string recordKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordKey);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where data_set_id = @data_set_id and record_key = @record_key";
        command.AddParameter("@data_set_id", dataSetId, DbType.Guid);
        command.AddParameter("@record_key", recordKey, DbType.String);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    public async Task UpsertAsync(DataRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_data_records (
                id, data_set_id, record_key, payload_json, as_of_date, schema_version_id, source_batch_id,
                source, checksum, created_at, updated_at)
            values (
                @id, @data_set_id, @record_key, cast(@payload_json as jsonb), @as_of_date, @schema_version_id,
                @source_batch_id, @source, @checksum, @created_at, @updated_at)
            on conflict (data_set_id, record_key) do update
            set id = excluded.id,
                payload_json = excluded.payload_json,
                as_of_date = excluded.as_of_date,
                schema_version_id = excluded.schema_version_id,
                source_batch_id = excluded.source_batch_id,
                source = excluded.source,
                checksum = excluded.checksum,
                updated_at = excluded.updated_at
            """;
        AddParameters(command, record);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DataRecord>> ListByDataSetAsync(
        Guid dataSetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where data_set_id = @data_set_id order by record_key asc, id asc";
        command.AddParameter("@data_set_id", dataSetId, DbType.Guid);
        var records = new List<DataRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) records.Add(ReadRecord(reader));
        return records;
    }

    private const string SelectBase = """
        select id, data_set_id, record_key, payload_json::text, as_of_date, schema_version_id, source_batch_id,
            source, checksum, created_at, updated_at
        from opk_data_records
        """;

    private static void AddParameters(System.Data.Common.DbCommand command, DataRecord record)
    {
        command.AddParameter("@id", record.Id, DbType.Guid);
        command.AddParameter("@data_set_id", record.DataSetId, DbType.Guid);
        command.AddParameter("@record_key", record.RecordKey, DbType.String);
        command.AddParameter("@payload_json", record.PayloadJson, DbType.String);
        command.AddParameter("@as_of_date", record.AsOfDate.ToDateTime(TimeOnly.MinValue), DbType.Date);
        command.AddParameter("@schema_version_id", record.SchemaVersionId, DbType.Guid);
        command.AddParameter("@source_batch_id", record.SourceBatchId, DbType.Guid);
        command.AddParameter("@source", record.Source, DbType.String);
        command.AddParameter("@checksum", record.Checksum, DbType.String);
        command.AddParameter("@created_at", record.CreatedAt, DbType.DateTimeOffset);
        command.AddParameter("@updated_at", record.UpdatedAt, DbType.DateTimeOffset);
    }

    private static DataRecord ReadRecord(System.Data.Common.DbDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
        DateOnly.FromDateTime(reader.GetDateTime(4)), reader.GetGuid(5), reader.GetGuid(6), reader.GetString(7),
        reader.GetString(8), reader.ReadDateTimeOffset(9), reader.ReadDateTimeOffset(10));
}
