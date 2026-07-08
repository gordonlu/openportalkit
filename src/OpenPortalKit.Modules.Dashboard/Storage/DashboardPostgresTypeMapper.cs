using System.Data;
using System.Data.Common;
using System.Text.Json;
using OpenPortalKit.Modules.Dashboard.Analytics;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Storage;

internal static class DashboardPostgresTypeMapper
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static DbParameter AddParameter(
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

    public static async Task<AnalyticsEvent> ReadAnalyticsEventAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var metadataJson = await reader.IsDBNullAsync(10, cancellationToken)
            ? "{}"
            : reader.GetString(10);
        var metadata = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(
            metadataJson,
            JsonOptions) ?? new Dictionary<string, string>();

        return new AnalyticsEvent(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            ReadDateTimeOffset(reader, 5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetValue(8).ToString(),
            reader.GetBoolean(9),
            metadata);
    }

    public static DashboardSnapshot ReadDashboardSnapshot(DbDataReader reader)
    {
        var summary = JsonSerializer.Deserialize<DashboardSummary>(
            reader.GetString(1),
            JsonOptions) ?? throw new InvalidOperationException("Dashboard snapshot summary_json could not be read.");

        return new DashboardSnapshot(
            reader.GetGuid(0),
            summary,
            ReadDateTimeOffset(reader, 2),
            ReadDateTimeOffset(reader, 3),
            reader.GetString(4));
    }

    public static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, int ordinal)
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
