using System.Text.Json;

namespace OpenPortalKit.Modules.Data.Datasets;

public static class CsvImportParser
{
    public static CsvImportParseResult Parse(string csv, string recordKeyColumn = "record_key")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordKeyColumn);

        if (string.IsNullOrWhiteSpace(csv))
        {
            return new CsvImportParseResult(
                Array.Empty<DataImportRow>(),
                new[] { new DataImportError(0, "csv_empty", "CSV content is empty.") });
        }

        var table = ParseTable(csv);

        if (table.Count == 0)
        {
            return new CsvImportParseResult(
                Array.Empty<DataImportRow>(),
                new[] { new DataImportError(0, "csv_empty", "CSV content is empty.") });
        }

        var headers = table[0].Select(header => header.Trim()).ToArray();
        var recordKeyIndex = Array.FindIndex(headers, header => string.Equals(
            header,
            recordKeyColumn,
            StringComparison.OrdinalIgnoreCase));

        if (recordKeyIndex < 0)
        {
            return new CsvImportParseResult(
                Array.Empty<DataImportRow>(),
                new[] { new DataImportError(1, "record_key_column_missing", $"CSV must include '{recordKeyColumn}' column.") });
        }

        var rows = new List<DataImportRow>();
        var errors = new List<DataImportError>();

        for (var rowIndex = 1; rowIndex < table.Count; rowIndex++)
        {
            var row = table[rowIndex];
            var rowNumber = rowIndex + 1;

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (row.Count != headers.Length)
            {
                errors.Add(new DataImportError(rowNumber, "csv_column_count_mismatch", "CSV row does not match header column count."));
                continue;
            }

            var recordKey = row[recordKeyIndex].Trim();
            var payload = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
            {
                if (columnIndex == recordKeyIndex)
                {
                    continue;
                }

                payload[headers[columnIndex]] = row[columnIndex];
            }

            rows.Add(new DataImportRow(recordKey, JsonSerializer.Serialize(payload), rowNumber));
        }

        return new CsvImportParseResult(rows, errors);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseTable(string csv)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var currentCell = new StringWriter();
        var inQuotes = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var current = csv[index];

            if (current == '"')
            {
                if (inQuotes && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    currentCell.Write('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && current == ',')
            {
                currentRow.Add(currentCell.ToString());
                currentCell.GetStringBuilder().Clear();
                continue;
            }

            if (!inQuotes && (current == '\n' || current == '\r'))
            {
                if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                {
                    index++;
                }

                currentRow.Add(currentCell.ToString());
                currentCell.GetStringBuilder().Clear();
                rows.Add(currentRow);
                currentRow = new List<string>();
                continue;
            }

            currentCell.Write(current);
        }

        currentRow.Add(currentCell.ToString());

        if (currentRow.Count > 1 || !string.IsNullOrEmpty(currentRow[0]))
        {
            rows.Add(currentRow);
        }

        return rows;
    }
}
