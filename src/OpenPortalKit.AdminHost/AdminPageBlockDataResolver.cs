using System.Text.Json;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Data.Datasets;

namespace OpenPortalKit.AdminHost;

internal sealed class AdminPageBlockDataResolver : IPageBlockDataResolver
{
    private readonly IDataSetStore _dataSetStore;
    private readonly IDataRecordStore _recordStore;

    public AdminPageBlockDataResolver(IDataSetStore dataSetStore, IDataRecordStore recordStore)
    {
        _dataSetStore = dataSetStore;
        _recordStore = recordStore;
    }

    public async Task<RenderedPageDataTable?> ResolveDataTableAsync(
        Guid siteId,
        string dataSetCode,
        int take,
        CancellationToken cancellationToken = default)
    {
        var detail = await new PublicDataSetQueryService(_dataSetStore, _recordStore)
            .FindByCodeAsync(siteId, dataSetCode, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var records = detail.Records.Take(take).ToArray();
        var columns = records
            .SelectMany(record => ReadPropertyNames(record.PayloadJson))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        var rows = records.Select(record =>
        {
            using var payload = JsonDocument.Parse(record.PayloadJson);
            return (IReadOnlyList<string>)columns.Select(column =>
                    payload.RootElement.TryGetProperty(column, out var value)
                        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
                        : string.Empty)
                .ToArray();
        }).ToArray();

        return new RenderedPageDataTable(
            detail.Name,
            detail.Description,
            columns,
            rows,
            "/api/public/datasets/" + Uri.EscapeDataString(detail.Code));
    }

    private static IReadOnlyList<string> ReadPropertyNames(string json)
    {
        using var payload = JsonDocument.Parse(json);
        return payload.RootElement.ValueKind == JsonValueKind.Object
            ? payload.RootElement.EnumerateObject().Select(property => property.Name).ToArray()
            : Array.Empty<string>();
    }
}
