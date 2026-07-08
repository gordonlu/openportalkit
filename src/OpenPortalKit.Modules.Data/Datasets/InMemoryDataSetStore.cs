namespace OpenPortalKit.Modules.Data.Datasets;

public sealed class InMemoryDataSetStore : IDataSetStore
{
    private readonly object _syncRoot = new();
    private readonly List<DataSet> _dataSets = new();
    private readonly List<DataSchemaVersion> _schemaVersions = new();

    public Task AddDataSetAsync(DataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _dataSets.RemoveAll(existing => existing.Id == dataSet.Id);
            _dataSets.Add(dataSet);
        }

        return Task.CompletedTask;
    }

    public Task<DataSet?> FindDataSetByCodeAsync(
        Guid siteId,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_dataSets.FirstOrDefault(dataSet =>
                dataSet.SiteId == siteId &&
                string.Equals(dataSet.Code, code, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<DataSet?> FindDataSetByIdAsync(Guid dataSetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_dataSets.FirstOrDefault(dataSet => dataSet.Id == dataSetId));
        }
    }

    public Task AddSchemaVersionAsync(
        DataSchemaVersion schemaVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schemaVersion);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _schemaVersions.RemoveAll(existing => existing.Id == schemaVersion.Id);
            _schemaVersions.Add(schemaVersion);
        }

        return Task.CompletedTask;
    }

    public Task<DataSchemaVersion?> FindSchemaVersionAsync(
        Guid schemaVersionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_schemaVersions.FirstOrDefault(schema => schema.Id == schemaVersionId));
        }
    }

    public Task<DataSchemaVersion?> FindLatestSchemaVersionAsync(
        Guid dataSetId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_schemaVersions
                .Where(schema => schema.DataSetId == dataSetId)
                .OrderByDescending(schema => schema.VersionNumber)
                .FirstOrDefault());
        }
    }
}
