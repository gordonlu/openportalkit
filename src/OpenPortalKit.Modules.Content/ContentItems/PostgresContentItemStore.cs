using System.Data;
using System.Text.Json;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed class PostgresContentItemStore : IContentItemStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresContentItemStore(IOpenPortalKitDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ContentItem> AddAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into opk_content_items (
                id, site_id, content_type_id, title, slug, summary, body, cover_asset_id, status, category_id,
                tags_json, author_id, source, published_at, scheduled_at, expires_at,
                created_by, updated_by, created_at, updated_at)
            values (
                @id, @site_id, @content_type_id, @title, @slug, @summary, @body, @cover_asset_id, @status,
                @category_id, cast(@tags_json as jsonb), @author_id, @source, @published_at, @scheduled_at, @expires_at,
                @created_by, @updated_by, @created_at, @updated_at)
            on conflict (id) do update
            set site_id = excluded.site_id,
                content_type_id = excluded.content_type_id,
                title = excluded.title,
                slug = excluded.slug,
                summary = excluded.summary,
                body = excluded.body,
                cover_asset_id = excluded.cover_asset_id,
                status = excluded.status,
                category_id = excluded.category_id,
                tags_json = excluded.tags_json,
                author_id = excluded.author_id,
                source = excluded.source,
                published_at = excluded.published_at,
                scheduled_at = excluded.scheduled_at,
                expires_at = excluded.expires_at,
                updated_by = excluded.updated_by,
                updated_at = excluded.updated_at
            """;
        AddItemParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var versionCommand = connection.CreateCommand();
        versionCommand.Transaction = transaction;
        versionCommand.CommandText = """
            insert into opk_content_item_versions (
                content_item_id, revision, snapshot_json, created_by, created_at)
            select @content_item_id, coalesce(max(revision), 0) + 1, cast(@snapshot_json as jsonb), @created_by, @created_at
            from opk_content_item_versions
            where content_item_id = @content_item_id
            """;
        versionCommand.AddParameter("@content_item_id", item.Id, DbType.Guid);
        versionCommand.AddParameter("@snapshot_json", JsonSerializer.Serialize(item), DbType.String);
        versionCommand.AddParameter("@created_by", item.UpdatedBy, DbType.Guid);
        versionCommand.AddParameter("@created_at", item.UpdatedAt, DbType.DateTimeOffset);
        await versionCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return item;
    }

    public async Task<ContentItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where id = @id";
        command.AddParameter("@id", id, DbType.Guid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task<ContentItem?> FindBySlugAsync(
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
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task<IReadOnlyList<ContentItem>> ListAsync(
        ContentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Take);

        var filters = new List<string>();
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        AddOptionalFilter(command, filters, query.SiteId, "site_id", "site_id");
        AddOptionalFilter(command, filters, query.ContentTypeId, "content_type_id", "content_type_id");
        AddOptionalFilter(command, filters, query.CategoryId, "category_id", "category_id");
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            filters.Add("tags_json @> cast(@tag_json as jsonb)");
            command.AddParameter("@tag_json", JsonSerializer.Serialize(new[] { query.Tag.Trim() }), DbType.String);
        }

        command.CommandText = SelectBase + BuildWhere(filters) +
            " order by coalesce(published_at, updated_at) desc, title asc, id asc limit @take offset @skip";
        command.AddParameter("@take", query.Take, DbType.Int32);
        command.AddParameter("@skip", query.Skip, DbType.Int32);
        return await ReadItemsAsync(command, cancellationToken);
    }

    public async Task<AdminContentListPage> ListAdminAsync(
        AdminContentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateAdminQuery(query);
        var filters = new List<string>();

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        AddAdminFilters(countCommand, filters, query);
        countCommand.CommandText = "select count(*) from opk_content_items" + BuildWhere(filters);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var listCommand = connection.CreateCommand();
        filters.Clear();
        AddAdminFilters(listCommand, filters, query);
        listCommand.CommandText = SelectBase + BuildWhere(filters) +
            " order by updated_at desc, title asc, id asc limit @take offset @skip";
        listCommand.AddParameter("@take", query.Take, DbType.Int32);
        listCommand.AddParameter("@skip", query.Skip, DbType.Int32);
        var items = await ReadItemsAsync(listCommand, cancellationToken);
        return new AdminContentListPage(items, totalCount);
    }

    public async Task<IReadOnlyList<ContentItem>> ListPublishedAsync(
        ContentListQuery query,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Take);

        var filters = new List<string>
        {
            "status = 'Published'",
            "published_at <= @as_of",
            "(expires_at is null or expires_at > @as_of)"
        };
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        AddOptionalFilter(command, filters, query.SiteId, "site_id", "site_id");
        AddOptionalFilter(command, filters, query.ContentTypeId, "content_type_id", "content_type_id");
        AddOptionalFilter(command, filters, query.CategoryId, "category_id", "category_id");
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            filters.Add("tags_json @> cast(@tag_json as jsonb)");
            command.AddParameter("@tag_json", JsonSerializer.Serialize(new[] { query.Tag.Trim() }), DbType.String);
        }
        command.CommandText = SelectBase + BuildWhere(filters) +
            " order by published_at desc, title asc, id asc limit @take offset @skip";
        command.AddParameter("@as_of", asOf, DbType.DateTimeOffset);
        command.AddParameter("@take", query.Take, DbType.Int32);
        command.AddParameter("@skip", query.Skip, DbType.Int32);
        return await ReadItemsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentItemRevision>> ListVersionsAsync(
        Guid contentItemId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select content_item_id, revision, snapshot_json::text, created_by, created_at
            from opk_content_item_versions
            where content_item_id = @content_item_id
            order by revision desc
            """;
        command.AddParameter("@content_item_id", contentItemId, DbType.Guid);
        var versions = new List<ContentItemRevision>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var snapshot = JsonSerializer.Deserialize<ContentItem>(reader.GetString(2)) ??
                throw new InvalidOperationException("Content item version snapshot could not be deserialized.");
            versions.Add(new ContentItemRevision(
                reader.GetGuid(0), reader.GetInt32(1), snapshot, reader.GetGuid(3), reader.ReadDateTimeOffset(4)));
        }
        return versions;
    }

    private const string SelectBase = """
        select id, site_id, content_type_id, title, slug, summary, body, cover_asset_id, status, category_id,
            tags_json::text, author_id, source, published_at, scheduled_at, expires_at,
            created_by, updated_by, created_at, updated_at
        from opk_content_items
        """;

    private static void ValidateAdminQuery(AdminContentListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        if (query.Take is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Admin page size must be between 1 and 100.");
        if (query.Search?.Trim().Length > 200)
            throw new ArgumentException("Admin content search must be at most 200 characters.", nameof(query));
    }

    private static void AddAdminFilters(
        System.Data.Common.DbCommand command,
        List<string> filters,
        AdminContentListQuery query)
    {
        AddOptionalFilter(command, filters, query.SiteId, "site_id", "site_id");
        AddOptionalFilter(command, filters, query.ContentTypeId, "content_type_id", "content_type_id");
        AddOptionalFilter(command, filters, query.AuthorId, "author_id", "author_id");
        if (query.Status is { } status)
        {
            filters.Add("status = @status");
            command.AddParameter("@status", status.ToString(), DbType.String);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filters.Add("(position(lower(@search) in lower(title)) > 0 or " +
                "position(lower(@search) in lower(slug)) > 0 or position(lower(@search) in lower(summary)) > 0)");
            command.AddParameter("@search", query.Search.Trim(), DbType.String);
        }
    }

    private static void AddOptionalFilter(
        System.Data.Common.DbCommand command,
        List<string> filters,
        Guid? value,
        string column,
        string parameter)
    {
        if (value is null) return;
        filters.Add($"{column} = @{parameter}");
        command.AddParameter("@" + parameter, value.Value, DbType.Guid);
    }

    private static string BuildWhere(IReadOnlyCollection<string> filters) =>
        filters.Count == 0 ? string.Empty : " where " + string.Join(" and ", filters);

    private static async Task<IReadOnlyList<ContentItem>> ReadItemsAsync(
        System.Data.Common.DbCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<ContentItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(ReadItem(reader));
        return items;
    }

    private static void AddItemParameters(System.Data.Common.DbCommand command, ContentItem item)
    {
        command.AddParameter("@id", item.Id, DbType.Guid);
        command.AddParameter("@site_id", item.SiteId, DbType.Guid);
        command.AddParameter("@content_type_id", item.ContentTypeId, DbType.Guid);
        command.AddParameter("@title", item.Title, DbType.String);
        command.AddParameter("@slug", item.Slug, DbType.String);
        command.AddParameter("@summary", item.Summary, DbType.String);
        command.AddParameter("@body", item.Body, DbType.String);
        command.AddParameter("@cover_asset_id", item.CoverAssetId, DbType.Guid);
        command.AddParameter("@status", item.Status.ToString(), DbType.String);
        command.AddParameter("@category_id", item.CategoryId, DbType.Guid);
        command.AddParameter("@tags_json", JsonSerializer.Serialize(item.Tags), DbType.String);
        command.AddParameter("@author_id", item.AuthorId, DbType.Guid);
        command.AddParameter("@source", item.Source, DbType.String);
        command.AddParameter("@published_at", item.PublishedAt, DbType.DateTimeOffset);
        command.AddParameter("@scheduled_at", item.ScheduledAt, DbType.DateTimeOffset);
        command.AddParameter("@expires_at", item.ExpiresAt, DbType.DateTimeOffset);
        command.AddParameter("@created_by", item.CreatedBy, DbType.Guid);
        command.AddParameter("@updated_by", item.UpdatedBy, DbType.Guid);
        command.AddParameter("@created_at", item.CreatedAt, DbType.DateTimeOffset);
        command.AddParameter("@updated_at", item.UpdatedAt, DbType.DateTimeOffset);
    }

    private static ContentItem ReadItem(System.Data.Common.DbDataReader reader)
    {
        var tags = JsonSerializer.Deserialize<string[]>(reader.GetString(10)) ?? Array.Empty<string>();
        return new ContentItem(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4),
            reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetGuid(7),
            Enum.Parse<ContentPublicationStatus>(reader.GetString(8), ignoreCase: true),
            reader.IsDBNull(9) ? null : reader.GetGuid(9), tags,
            reader.IsDBNull(11) ? null : reader.GetGuid(11), reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.ReadDateTimeOffset(13),
            reader.IsDBNull(14) ? null : reader.ReadDateTimeOffset(14),
            reader.IsDBNull(15) ? null : reader.ReadDateTimeOffset(15),
            reader.GetGuid(16), reader.GetGuid(17), reader.ReadDateTimeOffset(18), reader.ReadDateTimeOffset(19));
    }
}
