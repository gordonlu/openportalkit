using System.Data;
using System.Text.Json;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PostgresPageTemplateStore : IPageTemplateStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresPageTemplateStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<PageTemplate> SaveAsync(PageTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await UpsertTemplateAsync(connection, transaction, template, cancellationToken);
            await InsertVersionAsync(connection, transaction, template, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return template;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PageTemplate?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectTemplate + " where code = @code";
        command.AddParameter("@code", code, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTemplate(reader) : null;
    }

    public async Task<IReadOnlyList<PageTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectTemplate + " order by name asc, id asc";

        var templates = new List<PageTemplate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            templates.Add(ReadTemplate(reader));
        }

        return templates;
    }

    public async Task<IReadOnlyList<PageTemplateVersion>> ListVersionsAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select template_id, version, snapshot_json::text, created_by, created_at
            from opk_page_template_versions
            where template_id = @template_id
            order by version desc
            """;
        command.AddParameter("@template_id", templateId, DbType.Guid);

        var versions = new List<PageTemplateVersion>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var snapshot = JsonSerializer.Deserialize<PageTemplate>(reader.GetString(2)) ?? throw new InvalidOperationException(
                "Page template version snapshot could not be deserialized.");
            versions.Add(new PageTemplateVersion(
                reader.GetGuid(0),
                reader.GetInt32(1),
                snapshot,
                reader.GetGuid(3),
                reader.ReadDateTimeOffset(4)));
        }

        return versions;
    }

    private const string SelectTemplate = """
        select id, code, name, description, status, version, blocks_json::text,
            created_by, updated_by, created_at, updated_at
        from opk_page_templates
        """;

    private static async Task UpsertTemplateAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        PageTemplate template,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into opk_page_templates (
                id, code, name, description, status, version, blocks_json,
                created_by, updated_by, created_at, updated_at)
            values (
                @id, @code, @name, @description, @status, @version, cast(@blocks_json as jsonb),
                @created_by, @updated_by, @created_at, @updated_at)
            on conflict (id) do update
            set code = excluded.code,
                name = excluded.name,
                description = excluded.description,
                status = excluded.status,
                version = excluded.version,
                blocks_json = excluded.blocks_json,
                updated_by = excluded.updated_by,
                updated_at = excluded.updated_at
            """;
        AddTemplateParameters(command, template);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertVersionAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        PageTemplate template,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into opk_page_template_versions (
                template_id, version, snapshot_json, created_by, created_at)
            values (@template_id, @version, cast(@snapshot_json as jsonb), @created_by, @created_at)
            """;
        command.AddParameter("@template_id", template.Id, DbType.Guid);
        command.AddParameter("@version", template.Version, DbType.Int32);
        command.AddParameter("@snapshot_json", JsonSerializer.Serialize(template), DbType.String);
        command.AddParameter("@created_by", template.UpdatedBy, DbType.Guid);
        command.AddParameter("@created_at", template.UpdatedAt, DbType.DateTimeOffset);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddTemplateParameters(System.Data.Common.DbCommand command, PageTemplate template)
    {
        command.AddParameter("@id", template.Id, DbType.Guid);
        command.AddParameter("@code", template.Code, DbType.String);
        command.AddParameter("@name", template.Name, DbType.String);
        command.AddParameter("@description", template.Description, DbType.String);
        command.AddParameter("@status", template.Status.ToString(), DbType.String);
        command.AddParameter("@version", template.Version, DbType.Int32);
        command.AddParameter("@blocks_json", JsonSerializer.Serialize(template.Blocks), DbType.String);
        command.AddParameter("@created_by", template.CreatedBy, DbType.Guid);
        command.AddParameter("@updated_by", template.UpdatedBy, DbType.Guid);
        command.AddParameter("@created_at", template.CreatedAt, DbType.DateTimeOffset);
        command.AddParameter("@updated_at", template.UpdatedAt, DbType.DateTimeOffset);
    }

    private static PageTemplate ReadTemplate(System.Data.Common.DbDataReader reader)
    {
        var blocks = JsonSerializer.Deserialize<BlockInstance[]>(reader.GetString(6)) ?? Array.Empty<BlockInstance>();
        return new PageTemplate(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            Enum.Parse<PageTemplateStatus>(reader.GetString(4), ignoreCase: true),
            reader.GetInt32(5),
            blocks,
            reader.GetGuid(7),
            reader.GetGuid(8),
            reader.ReadDateTimeOffset(9),
            reader.ReadDateTimeOffset(10));
    }
}
