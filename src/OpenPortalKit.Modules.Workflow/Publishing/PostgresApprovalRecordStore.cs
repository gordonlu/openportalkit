using System.Data;
using OpenPortalKit.Kernel.Persistence;

namespace OpenPortalKit.Modules.Workflow.Publishing;

public sealed class PostgresApprovalRecordStore : IApprovalRecordStore
{
    private readonly IOpenPortalKitDbConnectionFactory _connectionFactory;

    public PostgresApprovalRecordStore(IOpenPortalKitDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task AddAsync(ApprovalRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into opk_approval_records (
                id, workflow_item_id, target_type, target_id, actor_id, action, from_state, to_state, comment, occurred_at)
            values (
                @id, @workflow_item_id, @target_type, @target_id, @actor_id, @action, @from_state, @to_state,
                @comment, @occurred_at)
            """;
        command.AddParameter("@id", record.Id, DbType.Guid);
        command.AddParameter("@workflow_item_id", record.WorkflowItemId, DbType.Guid);
        command.AddParameter("@target_type", record.TargetType, DbType.String);
        command.AddParameter("@target_id", record.TargetId, DbType.String);
        command.AddParameter("@actor_id", record.ActorId, DbType.Guid);
        command.AddParameter("@action", record.Action.ToString(), DbType.String);
        command.AddParameter("@from_state", record.FromState.ToString(), DbType.String);
        command.AddParameter("@to_state", record.ToState.ToString(), DbType.String);
        command.AddParameter("@comment", record.Comment, DbType.String);
        command.AddParameter("@occurred_at", record.OccurredAt, DbType.DateTimeOffset);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalRecord>> FindByTargetAsync(
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, workflow_item_id, target_type, target_id, actor_id, action, from_state, to_state, comment, occurred_at
            from opk_approval_records
            where target_type = @target_type and target_id = @target_id
            order by occurred_at desc, id desc
            """;
        command.AddParameter("@target_type", targetType, DbType.String);
        command.AddParameter("@target_id", targetId, DbType.String);
        var records = new List<ApprovalRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ApprovalRecord(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetGuid(4),
                Enum.Parse<WorkflowAction>(reader.GetString(5), true),
                Enum.Parse<WorkflowState>(reader.GetString(6), true),
                Enum.Parse<WorkflowState>(reader.GetString(7), true),
                reader.IsDBNull(8) ? null : reader.GetString(8), reader.ReadDateTimeOffset(9)));
        }
        return records;
    }
}
