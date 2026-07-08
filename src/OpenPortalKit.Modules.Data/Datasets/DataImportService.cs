using System.Text.Json;

namespace OpenPortalKit.Modules.Data.Datasets;

public sealed class DataImportService
{
    private readonly IDataSetStore _dataSetStore;
    private readonly IDataRecordStore _recordStore;

    public DataImportService(IDataSetStore dataSetStore, IDataRecordStore recordStore)
    {
        _dataSetStore = dataSetStore ?? throw new ArgumentNullException(nameof(dataSetStore));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
    }

    public async Task<DataImportResult> ImportAsync(
        DataImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Source);
        ArgumentNullException.ThrowIfNull(request.Rows);

        var importedAt = request.ImportedAt ?? DateTimeOffset.UtcNow;
        var errors = await ValidateAsync(request, cancellationToken);
        var batchId = Guid.NewGuid();
        var batchChecksum = BuildBatchChecksum(request);
        var batchStatus = errors.Count > 0
            ? DataImportBatchStatus.Failed
            : request.DryRun
                ? DataImportBatchStatus.DryRunSucceeded
                : DataImportBatchStatus.Completed;

        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var records = new List<DataRecord>();

        if (errors.Count == 0)
        {
            foreach (var row in request.Rows)
            {
                var canonicalPayload = DataChecksum.CanonicalizeJson(row.PayloadJson);
                var checksum = DataChecksum.FromText(canonicalPayload);
                var existing = await _recordStore.FindByKeyAsync(request.DataSetId, row.RecordKey, cancellationToken);

                if (existing is null)
                {
                    created++;
                }
                else if (string.Equals(existing.Checksum, checksum, StringComparison.Ordinal))
                {
                    unchanged++;
                }
                else
                {
                    updated++;
                }

                var record = new DataRecord(
                    existing?.Id ?? Guid.NewGuid(),
                    request.DataSetId,
                    row.RecordKey,
                    canonicalPayload,
                    request.AsOfDate,
                    request.SchemaVersionId,
                    batchId,
                    request.Source,
                    checksum,
                    existing?.CreatedAt ?? importedAt,
                    importedAt);

                records.Add(record);

                if (!request.DryRun)
                {
                    await _recordStore.UpsertAsync(record, cancellationToken);
                }
            }
        }

        var batch = new DataImportBatch(
            batchId,
            request.DataSetId,
            request.SchemaVersionId,
            request.Source,
            request.SourceFileName,
            request.AsOfDate,
            importedAt,
            request.ActorId,
            request.Rows.Count,
            created,
            updated,
            unchanged,
            errors.Count,
            batchChecksum,
            batchStatus);

        var qualityReport = new DataQualityReport(
            Guid.NewGuid(),
            request.DataSetId,
            batchId,
            request.Rows.Count,
            errors.Count,
            errors,
            importedAt);

        return new DataImportResult(
            errors.Count == 0,
            request.DryRun,
            batch,
            qualityReport,
            records);
    }

    private async Task<IReadOnlyList<DataImportError>> ValidateAsync(
        DataImportRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<DataImportError>();
        var dataSet = await _dataSetStore.FindDataSetByIdAsync(request.DataSetId, cancellationToken);
        var schemaVersion = await _dataSetStore.FindSchemaVersionAsync(request.SchemaVersionId, cancellationToken);

        if (dataSet is null)
        {
            errors.Add(new DataImportError(0, "dataset_not_found", "DataSet was not found."));
        }

        if (schemaVersion is null || schemaVersion.DataSetId != request.DataSetId)
        {
            errors.Add(new DataImportError(0, "schema_version_not_found", "Schema version was not found for this DataSet."));
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < request.Rows.Count; index++)
        {
            var row = request.Rows[index];
            var rowNumber = index + 1;

            if (string.IsNullOrWhiteSpace(row.RecordKey))
            {
                errors.Add(new DataImportError(rowNumber, "record_key_required", "Record key is required."));
                continue;
            }

            if (!keys.Add(row.RecordKey))
            {
                errors.Add(new DataImportError(rowNumber, "duplicate_record_key", "Record key appears more than once in this import.", row.RecordKey));
            }

            if (string.IsNullOrWhiteSpace(row.PayloadJson))
            {
                errors.Add(new DataImportError(rowNumber, "payload_required", "Payload JSON is required.", row.RecordKey));
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(row.PayloadJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new DataImportError(rowNumber, "payload_must_be_object", "Payload JSON must be an object.", row.RecordKey));
                }
            }
            catch (JsonException exception)
            {
                errors.Add(new DataImportError(rowNumber, "payload_invalid_json", exception.Message, row.RecordKey));
            }
        }

        return errors;
    }

    private static string BuildBatchChecksum(DataImportRequest request)
    {
        var canonicalRows = request.Rows
            .OrderBy(row => row.RecordKey, StringComparer.Ordinal)
            .Select(row =>
            {
                try
                {
                    return $"{row.RecordKey}:{DataChecksum.CanonicalizeJson(row.PayloadJson)}";
                }
                catch (JsonException)
                {
                    return $"{row.RecordKey}:{row.PayloadJson}";
                }
            });

        return DataChecksum.FromText(string.Join('\n', canonicalRows));
    }
}
