using System.Data;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Data.Datasets;

public sealed class PostgresDataSetStore : IDataSetStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresDataSetStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AddDataSetAsync(DataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_data_sets (id, site_id, code, name, description, is_public, created_at, updated_at)
            values (@id, @site_id, @code, @name, @description, @is_public, @created_at, @updated_at)
            on conflict (id) do update
            set site_id = excluded.site_id,
                code = excluded.code,
                name = excluded.name,
                description = excluded.description,
                is_public = excluded.is_public,
                updated_at = excluded.updated_at
            """;
        AddDataSetParameters(command, dataSet);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DataSet>> ListDataSetsAsync(
        Guid? siteId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectDataSet +
            (siteId is null ? " order by code asc, id asc" : " where site_id = @site_id order by code asc, id asc");
        if (siteId is not null) command.AddParameter("@site_id", siteId.Value, DbType.Guid);
        return await ReadDataSetsAsync(command, cancellationToken);
    }

    public async Task<DataSet?> FindDataSetByCodeAsync(
        Guid siteId,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectDataSet + " where site_id = @site_id and code = @code";
        command.AddParameter("@site_id", siteId, DbType.Guid);
        command.AddParameter("@code", code, DbType.String);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDataSet(reader) : null;
    }

    public async Task<DataSet?> FindDataSetByIdAsync(
        Guid dataSetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectDataSet + " where id = @id";
        command.AddParameter("@id", dataSetId, DbType.Guid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDataSet(reader) : null;
    }

    public async Task AddSchemaVersionAsync(
        DataSchemaVersion schemaVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schemaVersion);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_data_schema_versions (
                id, data_set_id, version_number, schema_json, checksum, created_at)
            values (@id, @data_set_id, @version_number, cast(@schema_json as jsonb), @checksum, @created_at)
            on conflict (id) do nothing
            """;
        AddSchemaParameters(command, schemaVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DataSchemaVersion?> FindSchemaVersionAsync(
        Guid schemaVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSchema + " where id = @id";
        command.AddParameter("@id", schemaVersionId, DbType.Guid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSchema(reader) : null;
    }

    public async Task<DataSchemaVersion?> FindLatestSchemaVersionAsync(
        Guid dataSetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSchema +
            " where data_set_id = @data_set_id order by version_number desc limit 1";
        command.AddParameter("@data_set_id", dataSetId, DbType.Guid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSchema(reader) : null;
    }

    private const string SelectDataSet = """
        select id, site_id, code, name, description, is_public, created_at, updated_at
        from opk_data_sets
        """;

    private const string SelectSchema = """
        select id, data_set_id, version_number, schema_json::text, checksum, created_at
        from opk_data_schema_versions
        """;

    private static void AddDataSetParameters(System.Data.Common.DbCommand command, DataSet dataSet)
    {
        command.AddParameter("@id", dataSet.Id, DbType.Guid);
        command.AddParameter("@site_id", dataSet.SiteId, DbType.Guid);
        command.AddParameter("@code", dataSet.Code, DbType.String);
        command.AddParameter("@name", dataSet.Name, DbType.String);
        command.AddParameter("@description", dataSet.Description, DbType.String);
        command.AddParameter("@is_public", dataSet.IsPublic, DbType.Boolean);
        command.AddParameter("@created_at", dataSet.CreatedAt, DbType.DateTimeOffset);
        command.AddParameter("@updated_at", dataSet.UpdatedAt, DbType.DateTimeOffset);
    }

    private static void AddSchemaParameters(System.Data.Common.DbCommand command, DataSchemaVersion schema)
    {
        command.AddParameter("@id", schema.Id, DbType.Guid);
        command.AddParameter("@data_set_id", schema.DataSetId, DbType.Guid);
        command.AddParameter("@version_number", schema.VersionNumber, DbType.Int32);
        command.AddParameter("@schema_json", schema.SchemaJson, DbType.String);
        command.AddParameter("@checksum", schema.Checksum, DbType.String);
        command.AddParameter("@created_at", schema.CreatedAt, DbType.DateTimeOffset);
    }

    private static async Task<IReadOnlyList<DataSet>> ReadDataSetsAsync(
        System.Data.Common.DbCommand command,
        CancellationToken cancellationToken)
    {
        var dataSets = new List<DataSet>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) dataSets.Add(ReadDataSet(reader));
        return dataSets;
    }

    private static DataSet ReadDataSet(System.Data.Common.DbDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
        reader.GetBoolean(5), reader.ReadDateTimeOffset(6), reader.ReadDateTimeOffset(7));

    private static DataSchemaVersion ReadSchema(System.Data.Common.DbDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4),
        reader.ReadDateTimeOffset(5));
}
