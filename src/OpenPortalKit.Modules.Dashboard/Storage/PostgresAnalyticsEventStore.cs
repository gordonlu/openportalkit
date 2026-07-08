using System.Data;
using System.Text.Json;
using OpenPortalKit.Modules.Dashboard.Analytics;

namespace OpenPortalKit.Modules.Dashboard.Storage;

public sealed class PostgresAnalyticsEventStore : IAnalyticsEventStore
{
    private readonly IDashboardDbConnectionFactory _connectionFactory;

    public PostgresAnalyticsEventStore(IDashboardDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AddAsync(
        AnalyticsEvent analyticsEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = DashboardPostgresSql.InsertAnalyticsEvent;
        DashboardPostgresTypeMapper.AddParameter(command, "@id", analyticsEvent.Id, DbType.Guid);
        DashboardPostgresTypeMapper.AddParameter(command, "@site_id", analyticsEvent.SiteId, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@event_type", analyticsEvent.EventType, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@path", analyticsEvent.Path, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@hashed_session_id", analyticsEvent.HashedSessionId, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@occurred_at", analyticsEvent.OccurredAt, DbType.DateTimeOffset);
        DashboardPostgresTypeMapper.AddParameter(command, "@referrer", analyticsEvent.Referrer, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@user_agent", analyticsEvent.UserAgent, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@anonymized_ip_address", analyticsEvent.AnonymizedIpAddress, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@is_bot", analyticsEvent.IsBot, DbType.Boolean);
        DashboardPostgresTypeMapper.AddParameter(
            command,
            "@metadata_json",
            JsonSerializer.Serialize(analyticsEvent.Metadata, DashboardPostgresTypeMapper.JsonOptions),
            DbType.String);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnalyticsEvent>> ListAsync(
        AnalyticsEventQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Take);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var where = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.SiteId))
        {
            where.Add("site_id = @site_id");
            DashboardPostgresTypeMapper.AddParameter(command, "@site_id", query.SiteId, DbType.String);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            where.Add("event_type = @event_type");
            DashboardPostgresTypeMapper.AddParameter(command, "@event_type", query.EventType, DbType.String);
        }

        if (query.From is not null)
        {
            where.Add("occurred_at >= @from");
            DashboardPostgresTypeMapper.AddParameter(command, "@from", query.From.Value, DbType.DateTimeOffset);
        }

        if (query.To is not null)
        {
            where.Add("occurred_at <= @to");
            DashboardPostgresTypeMapper.AddParameter(command, "@to", query.To.Value, DbType.DateTimeOffset);
        }

        DashboardPostgresTypeMapper.AddParameter(command, "@take", query.Take, DbType.Int32);
        DashboardPostgresTypeMapper.AddParameter(command, "@skip", query.Skip, DbType.Int32);
        command.CommandText = """
            select
                id,
                site_id,
                event_type,
                path,
                hashed_session_id,
                occurred_at,
                referrer,
                user_agent,
                anonymized_ip_address,
                is_bot,
                metadata_json::text
            from opk_analytics_events
            """ +
            (where.Count == 0 ? string.Empty : " where " + string.Join(" and ", where)) +
            """
             order by occurred_at desc, id desc
             limit @take offset @skip
            """;

        var results = new List<AnalyticsEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(await DashboardPostgresTypeMapper.ReadAnalyticsEventAsync(reader, cancellationToken));
        }

        return results;
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = DashboardPostgresSql.DeleteAnalyticsEventsOlderThan;
        DashboardPostgresTypeMapper.AddParameter(command, "@cutoff", cutoff, DbType.DateTimeOffset);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
