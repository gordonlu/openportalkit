using System.Text.Json;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Data.Datasets;

internal sealed class ApiPublicPageBlockDataResolver : IPageBlockDataResolver
{
    private readonly IDataSetStore _dataSetStore;
    private readonly IDataRecordStore _recordStore;

    public ApiPublicPageBlockDataResolver(IDataSetStore dataSetStore, IDataRecordStore recordStore)
    {
        _dataSetStore = dataSetStore ?? throw new ArgumentNullException(nameof(dataSetStore));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
    }

    public async Task<RenderedPageDataTable?> ResolveDataTableAsync(
        Guid siteId,
        string dataSetCode,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = new PublicDataSetQueryService(_dataSetStore, _recordStore);
        var dataSet = await query.FindByCodeAsync(siteId, dataSetCode, cancellationToken);
        if (dataSet is null)
        {
            return null;
        }

        var records = dataSet.Records.Take(take).ToArray();
        var columns = new List<string> { "Record key", "As of" };
        foreach (var record in records)
        {
            using var payload = JsonDocument.Parse(record.PayloadJson);
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var property in payload.RootElement.EnumerateObject())
            {
                if (!columns.Contains(property.Name, StringComparer.OrdinalIgnoreCase) && columns.Count < 10)
                {
                    columns.Add(property.Name);
                }
            }
        }

        var rows = new List<IReadOnlyList<string>>();
        foreach (var record in records)
        {
            using var payload = JsonDocument.Parse(record.PayloadJson);
            rows.Add(columns.Select(column => ResolveCell(record, payload.RootElement, column)).ToArray());
        }

        return new RenderedPageDataTable(
            dataSet.Name,
            dataSet.Description,
            columns,
            rows,
            "/api/public/datasets/" + Uri.EscapeDataString(dataSet.Code));
    }

    private static string ResolveCell(PublicDataRecord record, JsonElement payload, string column)
    {
        if (string.Equals(column, "Record key", StringComparison.Ordinal))
        {
            return record.RecordKey;
        }

        if (string.Equals(column, "As of", StringComparison.Ordinal))
        {
            return record.AsOfDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return payload.TryGetProperty(column, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
            : string.Empty;
    }
}
