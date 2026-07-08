using System.Text;

namespace OpenPortalKit.Modules.Dashboard.Summaries;

public static class DashboardSummaryChecksum
{
    public static string Compute(DashboardSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var builder = new StringBuilder();
        foreach (var card in summary.Cards
            .OrderBy(card => card.Area)
            .ThenBy(card => card.Code, StringComparer.Ordinal))
        {
            builder.Append(card.Area).Append('|').Append(card.Code).Append('|');
            foreach (var metric in card.Metrics.OrderBy(metric => metric.Code, StringComparer.Ordinal))
            {
                builder
                    .Append(metric.Code)
                    .Append('=')
                    .Append(metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append(';');
            }

            foreach (var alert in card.Alerts.OrderBy(alert => alert.Code, StringComparer.Ordinal))
            {
                builder
                    .Append(alert.Code)
                    .Append('=')
                    .Append((int)alert.Level)
                    .Append(';');
            }
        }

        return StableHash(builder.ToString());
    }

    private static string StableHash(string input)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offset;
        foreach (var value in Encoding.UTF8.GetBytes(input))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
