namespace OpenPortalKit.Modules.Data.Datasets;

public sealed class InMemoryDataRecordStore : IDataRecordStore
{
    private readonly object _syncRoot = new();
    private readonly List<DataRecord> _records = new();

    public Task<DataRecord?> FindByKeyAsync(
        Guid dataSetId,
        string recordKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_records.FirstOrDefault(record =>
                record.DataSetId == dataSetId &&
                string.Equals(record.RecordKey, recordKey, StringComparison.Ordinal)));
        }
    }

    public Task UpsertAsync(DataRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _records.RemoveAll(existing =>
                existing.DataSetId == record.DataSetId &&
                string.Equals(existing.RecordKey, record.RecordKey, StringComparison.Ordinal));
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DataRecord>> ListByDataSetAsync(
        Guid dataSetId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyList<DataRecord>>(
                _records
                    .Where(record => record.DataSetId == dataSetId)
                    .OrderBy(record => record.RecordKey, StringComparer.Ordinal)
                    .ToArray());
        }
    }
}
