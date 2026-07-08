using System.Data;
using System.Text.Json;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Storage;

public sealed class PostgresDashboardSnapshotStore : IDashboardSnapshotStore
{
    private const int SchemaVersion = 1;
    private readonly IDashboardDbConnectionFactory _connectionFactory;

    public PostgresDashboardSnapshotStore(IDashboardDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<DashboardSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = DashboardPostgresSql.SelectLatestDashboardSnapshot;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? DashboardPostgresTypeMapper.ReadDashboardSnapshot(reader)
            : null;
    }

    public async Task SaveAsync(
        DashboardSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = DashboardPostgresSql.InsertDashboardSnapshot;

        var sourceModules = snapshot.Summary.Cards
            .SelectMany(card => card.Metrics.Select(metric => metric.SourceModule)
                .Concat(card.Alerts.Select(alert => alert.SourceModule)))
            .Concat(snapshot.Summary.Alerts.Select(alert => alert.SourceModule))
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(source => source, StringComparer.Ordinal)
            .ToArray();
        var alertCount = snapshot.Summary.Cards.Sum(card => card.Alerts.Count) + snapshot.Summary.Alerts.Count;

        DashboardPostgresTypeMapper.AddParameter(command, "@id", snapshot.Id, DbType.Guid);
        DashboardPostgresTypeMapper.AddParameter(command, "@generated_at", snapshot.Summary.GeneratedAt, DbType.DateTimeOffset);
        DashboardPostgresTypeMapper.AddParameter(command, "@created_at", snapshot.CreatedAt, DbType.DateTimeOffset);
        DashboardPostgresTypeMapper.AddParameter(command, "@expires_at", snapshot.ExpiresAt, DbType.DateTimeOffset);
        DashboardPostgresTypeMapper.AddParameter(command, "@source_checksum", snapshot.SourceChecksum, DbType.String);
        DashboardPostgresTypeMapper.AddParameter(
            command,
            "@summary_json",
            JsonSerializer.Serialize(snapshot.Summary, DashboardPostgresTypeMapper.JsonOptions),
            DbType.String);
        DashboardPostgresTypeMapper.AddParameter(
            command,
            "@source_modules_json",
            JsonSerializer.Serialize(sourceModules, DashboardPostgresTypeMapper.JsonOptions),
            DbType.String);
        DashboardPostgresTypeMapper.AddParameter(command, "@card_count", snapshot.Summary.Cards.Count, DbType.Int32);
        DashboardPostgresTypeMapper.AddParameter(command, "@alert_count", alertCount, DbType.Int32);
        DashboardPostgresTypeMapper.AddParameter(command, "@actionable_alert_count", snapshot.Summary.ActionableAlertCount, DbType.Int32);
        DashboardPostgresTypeMapper.AddParameter(command, "@schema_version", SchemaVersion, DbType.Int32);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
