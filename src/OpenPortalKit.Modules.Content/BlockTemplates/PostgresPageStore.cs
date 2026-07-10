using System.Data;
using System.Text.Json;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PostgresPageStore : IPageStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresPageStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<PortalPage> UpsertAsync(PortalPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_portal_pages (
                id, site_id, template_id, template_version, title, slug, summary, status, blocks_json,
                created_by, updated_by, created_at, updated_at, published_at)
            values (
                @id, @site_id, @template_id, @template_version, @title, @slug, @summary, @status,
                cast(@blocks_json as jsonb), @created_by, @updated_by, @created_at, @updated_at, @published_at)
            on conflict (id) do update
            set template_id = excluded.template_id,
                template_version = excluded.template_version,
                title = excluded.title,
                slug = excluded.slug,
                summary = excluded.summary,
                status = excluded.status,
                blocks_json = excluded.blocks_json,
                updated_by = excluded.updated_by,
                updated_at = excluded.updated_at,
                published_at = excluded.published_at
            """;
        AddParameters(command, page);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return page;
    }

    public async Task<PortalPage?> FindBySlugAsync(
        Guid siteId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where site_id = @site_id and slug = @slug";
        command.AddParameter("@site_id", siteId, DbType.Guid);
        command.AddParameter("@slug", slug, DbType.String);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPage(reader) : null;
    }

    public async Task<IReadOnlyList<PortalPage>> ListAsync(
        Guid? siteId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase +
            (siteId is null ? " order by title asc, id asc" : " where site_id = @site_id order by title asc, id asc");
        if (siteId is not null)
        {
            command.AddParameter("@site_id", siteId.Value, DbType.Guid);
        }

        var pages = new List<PortalPage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            pages.Add(ReadPage(reader));
        }

        return pages;
    }

    private const string SelectBase = """
        select id, site_id, template_id, template_version, title, slug, summary, status, blocks_json::text,
            created_by, updated_by, created_at, updated_at, published_at
        from opk_portal_pages
        """;

    private static void AddParameters(System.Data.Common.DbCommand command, PortalPage page)
    {
        command.AddParameter("@id", page.Id, DbType.Guid);
        command.AddParameter("@site_id", page.SiteId, DbType.Guid);
        command.AddParameter("@template_id", page.TemplateId, DbType.Guid);
        command.AddParameter("@template_version", page.TemplateVersion, DbType.Int32);
        command.AddParameter("@title", page.Title, DbType.String);
        command.AddParameter("@slug", page.Slug, DbType.String);
        command.AddParameter("@summary", page.Summary, DbType.String);
        command.AddParameter("@status", page.Status.ToString(), DbType.String);
        command.AddParameter("@blocks_json", JsonSerializer.Serialize(page.Blocks), DbType.String);
        command.AddParameter("@created_by", page.CreatedBy, DbType.Guid);
        command.AddParameter("@updated_by", page.UpdatedBy, DbType.Guid);
        command.AddParameter("@created_at", page.CreatedAt, DbType.DateTimeOffset);
        command.AddParameter("@updated_at", page.UpdatedAt, DbType.DateTimeOffset);
        command.AddParameter("@published_at", page.PublishedAt, DbType.DateTimeOffset);
    }

    private static PortalPage ReadPage(System.Data.Common.DbDataReader reader)
    {
        var blocks = JsonSerializer.Deserialize<BlockInstance[]>(reader.GetString(8)) ?? Array.Empty<BlockInstance>();
        return new PortalPage(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            Enum.Parse<PortalPageStatus>(reader.GetString(7), ignoreCase: true),
            blocks,
            reader.GetGuid(9),
            reader.GetGuid(10),
            reader.ReadDateTimeOffset(11),
            reader.ReadDateTimeOffset(12),
            reader.IsDBNull(13) ? null : reader.ReadDateTimeOffset(13));
    }
}
