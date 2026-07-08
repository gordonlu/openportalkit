using System.Globalization;
using System.Text;

namespace OpenPortalKit.Modules.Data.Datasets;

public static class CsvDataExporter
{
    private static readonly string[] Headers =
    {
        "record_key",
        "payload_json",
        "as_of_date",
        "schema_version_id",
        "source_batch_id",
        "source",
        "checksum",
        "updated_at"
    };

    public static string Export(IEnumerable<PublicDataRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Headers));

        foreach (var record in records.OrderBy(record => record.RecordKey, StringComparer.Ordinal))
        {
            builder.AppendJoin(',', new[]
            {
                Escape(record.RecordKey),
                Escape(record.PayloadJson),
                Escape(record.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Escape(record.SchemaVersionId.ToString("D")),
                Escape(record.SourceBatchId.ToString("D")),
                Escape(record.Source),
                Escape(record.Checksum),
                Escape(record.UpdatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))
            });
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains('"', StringComparison.Ordinal) ||
            value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
