namespace OpenPortalKit.Modules.Data.Datasets;

public interface IDataSetStore
{
    Task AddDataSetAsync(DataSet dataSet, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataSet>> ListDataSetsAsync(Guid? siteId = null, CancellationToken cancellationToken = default);
    Task<DataSet?> FindDataSetByCodeAsync(Guid siteId, string code, CancellationToken cancellationToken = default);
    Task<DataSet?> FindDataSetByIdAsync(Guid dataSetId, CancellationToken cancellationToken = default);
    Task AddSchemaVersionAsync(DataSchemaVersion schemaVersion, CancellationToken cancellationToken = default);
    Task<DataSchemaVersion?> FindSchemaVersionAsync(Guid schemaVersionId, CancellationToken cancellationToken = default);
    Task<DataSchemaVersion?> FindLatestSchemaVersionAsync(Guid dataSetId, CancellationToken cancellationToken = default);
}
