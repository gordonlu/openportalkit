using System.Data;
using System.Data.Common;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class PostgresAgentOutputArtifactStore : IAgentOutputArtifactStore
{
    private readonly IAgentOutputDbConnectionFactory _connectionFactory;

    public PostgresAgentOutputArtifactStore(IAgentOutputDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task UpsertAsync(
        AgentOutputArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AgentOutputPostgresSql.UpsertArtifact;
        AddParameter(command, "@path", artifact.Path, DbType.String);
        AddParameter(command, "@content_type", artifact.ContentType, DbType.String);
        AddParameter(command, "@body", artifact.Body, DbType.String);
        AddParameter(command, "@source_id", artifact.SourceId, DbType.String);
        AddParameter(command, "@source_kind", artifact.SourceKind, DbType.String);
        AddParameter(command, "@schema_version", artifact.SchemaVersion, DbType.String);
        AddParameter(command, "@checksum", artifact.Checksum, DbType.String);
        AddParameter(command, "@generated_at", artifact.GeneratedAt, DbType.DateTimeOffset);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AgentOutputArtifact?> FindByPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AgentOutputPostgresSql.SelectArtifactByPath;
        AddParameter(command, "@path", path, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadArtifact(reader)
            : null;
    }

    public async Task<IReadOnlyList<AgentOutputArtifact>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AgentOutputPostgresSql.SelectArtifacts;

        var results = new List<AgentOutputArtifact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadArtifact(reader));
        }

        return results;
    }

    private static DbParameter AddParameter(
        DbCommand command,
        string name,
        object? value,
        DbType? dbType = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        if (dbType is not null)
        {
            parameter.DbType = dbType.Value;
        }

        command.Parameters.Add(parameter);
        return parameter;
    }

    private static AgentOutputArtifact ReadArtifact(DbDataReader reader)
    {
        return new AgentOutputArtifact(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            ReadDateTimeOffset(reader, 7));
    }

    private static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => DateTimeOffset.Parse(value.ToString()!, System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
