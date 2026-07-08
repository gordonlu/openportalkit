namespace OpenPortalKit.Modules.Data.Datasets;

public sealed class PublicDataSetQueryService
{
    private readonly IDataSetStore _dataSetStore;
    private readonly IDataRecordStore _recordStore;

    public PublicDataSetQueryService(IDataSetStore dataSetStore, IDataRecordStore recordStore)
    {
        _dataSetStore = dataSetStore ?? throw new ArgumentNullException(nameof(dataSetStore));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
    }

    public async Task<PublicDataSetDetail?> FindByCodeAsync(
        Guid siteId,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var dataSet = await _dataSetStore.FindDataSetByCodeAsync(siteId, code, cancellationToken);

        if (dataSet is null || !dataSet.IsPublic)
        {
            return null;
        }

        var records = await _recordStore.ListByDataSetAsync(dataSet.Id, cancellationToken);

        return new PublicDataSetDetail(
            dataSet.Code,
            dataSet.Name,
            dataSet.Description,
            records.Select(record => new PublicDataRecord(
                record.RecordKey,
                record.PayloadJson,
                record.AsOfDate,
                record.SchemaVersionId,
                record.SourceBatchId,
                record.Source,
                record.Checksum,
                record.UpdatedAt)).ToArray());
    }

    public async Task<PublicDataSetSchema?> FindSchemaByCodeAsync(
        Guid siteId,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var dataSet = await _dataSetStore.FindDataSetByCodeAsync(siteId, code, cancellationToken);

        if (dataSet is null || !dataSet.IsPublic)
        {
            return null;
        }

        var schema = await _dataSetStore.FindLatestSchemaVersionAsync(dataSet.Id, cancellationToken);

        return schema is null
            ? null
            : new PublicDataSetSchema(
                dataSet.Code,
                schema.VersionNumber,
                schema.SchemaJson,
                schema.Checksum,
                schema.CreatedAt);
    }

    public async Task<PublicDataRecord?> FindRecordByKeyAsync(
        Guid siteId,
        string code,
        string recordKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordKey);

        var dataSet = await _dataSetStore.FindDataSetByCodeAsync(siteId, code, cancellationToken);

        if (dataSet is null || !dataSet.IsPublic)
        {
            return null;
        }

        var record = await _recordStore.FindByKeyAsync(dataSet.Id, recordKey, cancellationToken);

        return record is null
            ? null
            : new PublicDataRecord(
                record.RecordKey,
                record.PayloadJson,
                record.AsOfDate,
                record.SchemaVersionId,
                record.SourceBatchId,
                record.Source,
                record.Checksum,
                record.UpdatedAt);
    }
}
