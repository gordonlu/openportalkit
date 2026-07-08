using System.Text.Json;

namespace OpenPortalKit.Modules.Data.Datasets;

public static class DataSnapshotGenerator
{
    public static DataSnapshot CreateJsonSnapshot(
        Guid dataSetId,
        Guid schemaVersionId,
        Guid sourceBatchId,
        PublicDataSetDetail detail,
        DateTimeOffset? generatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var content = JsonSerializer.Serialize(new
        {
            detail.Code,
            detail.Name,
            detail.Description,
            Records = detail.Records.OrderBy(record => record.RecordKey, StringComparer.Ordinal).ToArray()
        });

        return new DataSnapshot(
            Guid.NewGuid(),
            dataSetId,
            schemaVersionId,
            sourceBatchId,
            "json",
            content,
            DataChecksum.FromText(content),
            generatedAt ?? DateTimeOffset.UtcNow);
    }
}
