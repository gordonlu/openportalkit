using System.Data;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed class PostgresPublishingWorkflowItemStore : IPublishingWorkflowItemStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresPublishingWorkflowItemStore(IOpenPortalKitDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task AddAsync(
        PublishingWorkflowItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_publishing_workflow_items (
                id, target_type, target_id, state, version_number, created_by, updated_by, created_at, updated_at,
                review_comment, approved_at, published_at, scheduled_at, archived_at)
            values (
                @id, @target_type, @target_id, @state, @version_number, @created_by, @updated_by, @created_at,
                @updated_at, @review_comment, @approved_at, @published_at, @scheduled_at, @archived_at)
            """;
        AddParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> TryUpdateAsync(
        PublishingWorkflowItem item,
        WorkflowState expectedState,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update opk_publishing_workflow_items
            set state = @state,
                version_number = @version_number,
                updated_by = @updated_by,
                updated_at = @updated_at,
                review_comment = @review_comment,
                approved_at = @approved_at,
                published_at = @published_at,
                scheduled_at = @scheduled_at,
                archived_at = @archived_at
            where id = @id and state = @expected_state and updated_at = @expected_updated_at
            """;
        AddParameters(command, item);
        command.AddParameter("@expected_state", expectedState.ToString(), DbType.String);
        command.AddParameter("@expected_updated_at", expectedUpdatedAt, DbType.DateTimeOffset);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<PublishingWorkflowItem?> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + " where target_type = @target_type and target_id = @target_id";
        command.AddParameter("@target_type", targetType, DbType.String);
        command.AddParameter("@target_id", targetId, DbType.String);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task<IReadOnlyList<PublishingWorkflowItem>> ListScheduledDueAsync(
        DateTimeOffset asOf,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(take));
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectBase + "\n" + """
            where state = 'Approved' and scheduled_at is not null and scheduled_at <= @as_of
            order by scheduled_at asc, id asc
            limit @take
            """;
        command.AddParameter("@as_of", asOf, DbType.DateTimeOffset);
        command.AddParameter("@take", take, DbType.Int32);
        var items = new List<PublishingWorkflowItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(ReadItem(reader));
        return items;
    }

    private const string SelectBase = """
        select id, target_type, target_id, state, version_number, created_by, updated_by, created_at, updated_at,
            review_comment, approved_at, published_at, scheduled_at, archived_at
        from opk_publishing_workflow_items
        """;

    private static void AddParameters(System.Data.Common.DbCommand command, PublishingWorkflowItem item)
    {
        command.AddParameter("@id", item.Id, DbType.Guid);
        command.AddParameter("@target_type", item.TargetType, DbType.String);
        command.AddParameter("@target_id", item.TargetId, DbType.String);
        command.AddParameter("@state", item.State.ToString(), DbType.String);
        command.AddParameter("@version_number", item.VersionNumber, DbType.Int32);
        command.AddParameter("@created_by", item.CreatedBy, DbType.Guid);
        command.AddParameter("@updated_by", item.UpdatedBy, DbType.Guid);
        command.AddParameter("@created_at", item.CreatedAt, DbType.DateTimeOffset);
        command.AddParameter("@updated_at", item.UpdatedAt, DbType.DateTimeOffset);
        command.AddParameter("@review_comment", item.ReviewComment, DbType.String);
        command.AddParameter("@approved_at", item.ApprovedAt, DbType.DateTimeOffset);
        command.AddParameter("@published_at", item.PublishedAt, DbType.DateTimeOffset);
        command.AddParameter("@scheduled_at", item.ScheduledAt, DbType.DateTimeOffset);
        command.AddParameter("@archived_at", item.ArchivedAt, DbType.DateTimeOffset);
    }

    private static PublishingWorkflowItem ReadItem(System.Data.Common.DbDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
        Enum.Parse<WorkflowState>(reader.GetString(3), ignoreCase: true), reader.GetInt32(4),
        reader.GetGuid(5), reader.GetGuid(6), reader.ReadDateTimeOffset(7), reader.ReadDateTimeOffset(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.ReadDateTimeOffset(10),
        reader.IsDBNull(11) ? null : reader.ReadDateTimeOffset(11),
        reader.IsDBNull(12) ? null : reader.ReadDateTimeOffset(12),
        reader.IsDBNull(13) ? null : reader.ReadDateTimeOffset(13));
}
