using System.Data;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.IndustryPacks;

public sealed class PostgresIndustryPackInstallationStore : IIndustryPackInstallationStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresIndustryPackInstallationStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<IndustryPackInstallation>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectInstallations + " order by pack_name asc";
        var results = new List<IndustryPackInstallation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add(ReadInstallation(reader));
        return results;
    }

    public async Task<IndustryPackInstallation?> FindAsync(
        string packName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectInstallations + " where pack_name = @pack_name";
        command.AddParameter("@pack_name", packName, DbType.String);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadInstallation(reader) : null;
    }

    public async Task<IReadOnlyList<IndustryPackResourceRegistration>> ListResourcesAsync(
        string packName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select pack_name, resource_path, resource_kind, checksum, registered_at
            from opk_industry_pack_resources
            where pack_name = @pack_name
            order by resource_path asc
            """;
        command.AddParameter("@pack_name", packName, DbType.String);
        var results = new List<IndustryPackResourceRegistration>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new IndustryPackResourceRegistration(
                reader.GetString(0), reader.GetString(1),
                Enum.Parse<IndustryPackResourceKind>(reader.GetString(2), true),
                reader.GetString(3), reader.ReadDateTimeOffset(4)));
        }

        return results;
    }

    public async Task SaveAsync(
        IndustryPackInstallation installation,
        IReadOnlyList<IndustryPackResourceRegistration> resources,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into opk_industry_pack_installations (
                    pack_name, version, manifest_checksum, is_enabled, updated_by, installed_at, updated_at)
                values (@pack_name, @version, @manifest_checksum, @is_enabled, @updated_by, @installed_at, @updated_at)
                on conflict (pack_name) do update
                set version = excluded.version,
                    manifest_checksum = excluded.manifest_checksum,
                    is_enabled = excluded.is_enabled,
                    updated_by = excluded.updated_by,
                    updated_at = excluded.updated_at
                """;
            command.AddParameter("@pack_name", installation.PackName, DbType.String);
            command.AddParameter("@version", installation.Version, DbType.String);
            command.AddParameter("@manifest_checksum", installation.ManifestChecksum, DbType.String);
            command.AddParameter("@is_enabled", installation.IsEnabled, DbType.Boolean);
            command.AddParameter("@updated_by", installation.UpdatedBy, DbType.Guid);
            command.AddParameter("@installed_at", installation.InstalledAt, DbType.DateTimeOffset);
            command.AddParameter("@updated_at", installation.UpdatedAt, DbType.DateTimeOffset);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "delete from opk_industry_pack_resources where pack_name = @pack_name";
            delete.AddParameter("@pack_name", installation.PackName, DbType.String);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var resource in resources)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into opk_industry_pack_resources (
                    pack_name, resource_path, resource_kind, checksum, registered_at)
                values (@pack_name, @resource_path, @resource_kind, @checksum, @registered_at)
                """;
            insert.AddParameter("@pack_name", resource.PackName, DbType.String);
            insert.AddParameter("@resource_path", resource.ResourcePath, DbType.String);
            insert.AddParameter("@resource_kind", resource.Kind.ToString(), DbType.String);
            insert.AddParameter("@checksum", resource.Checksum, DbType.String);
            insert.AddParameter("@registered_at", resource.RegisteredAt, DbType.DateTimeOffset);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private const string SelectInstallations = """
        select pack_name, version, manifest_checksum, is_enabled, updated_by, installed_at, updated_at
        from opk_industry_pack_installations
        """;

    private static IndustryPackInstallation ReadInstallation(System.Data.Common.DbDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetGuid(4),
        reader.ReadDateTimeOffset(5), reader.ReadDateTimeOffset(6));
}
