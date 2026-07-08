using System.Globalization;
using System.Text;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Observability;

public static class DashboardPrometheusExporter
{
    public static string Export(DashboardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new StringBuilder();
        builder.AppendLine("# HELP openportalkit_dashboard_metric Dashboard metric value.");
        builder.AppendLine("# TYPE openportalkit_dashboard_metric gauge");

        foreach (var card in snapshot.Summary.Cards)
        {
            foreach (var metric in card.Metrics)
            {
                builder
                    .Append("openportalkit_dashboard_metric")
                    .Append('{')
                    .Append("area=\"").Append(Escape(metric.Area.ToString())).Append("\",")
                    .Append("card=\"").Append(Escape(metric.CardCode)).Append("\",")
                    .Append("code=\"").Append(Escape(metric.Code)).Append("\",")
                    .Append("unit=\"").Append(Escape(metric.Unit)).Append("\",")
                    .Append("source=\"").Append(Escape(metric.SourceModule)).Append("\"")
                    .Append("} ")
                    .AppendLine(metric.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        builder.AppendLine("# HELP openportalkit_dashboard_alerts Dashboard alert count by level.");
        builder.AppendLine("# TYPE openportalkit_dashboard_alerts gauge");

        foreach (var group in snapshot.Summary.Cards
            .SelectMany(card => card.Alerts)
            .Concat(snapshot.Summary.Alerts)
            .GroupBy(alert => alert.Level)
            .OrderBy(group => group.Key))
        {
            builder
                .Append("openportalkit_dashboard_alerts")
                .Append('{')
                .Append("level=\"").Append(Escape(group.Key.ToString())).Append("\"")
                .Append("} ")
                .AppendLine(group.Count().ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine("# HELP openportalkit_dashboard_actionable_alerts Dashboard actionable alert count.");
        builder.AppendLine("# TYPE openportalkit_dashboard_actionable_alerts gauge");
        builder
            .Append("openportalkit_dashboard_actionable_alerts ")
            .AppendLine(snapshot.Summary.ActionableAlertCount.ToString(CultureInfo.InvariantCulture));

        builder.AppendLine("# HELP openportalkit_dashboard_snapshot_age_seconds Dashboard snapshot age in seconds.");
        builder.AppendLine("# TYPE openportalkit_dashboard_snapshot_age_seconds gauge");
        builder
            .Append("openportalkit_dashboard_snapshot_age_seconds ")
            .AppendLine(Math.Max(0, (DateTimeOffset.UtcNow - snapshot.CreatedAt).TotalSeconds)
                .ToString("0", CultureInfo.InvariantCulture));

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
