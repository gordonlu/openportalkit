namespace OpenPortalKit.Modules.Data.Datasets;

public interface IDataRecordStore
{
    Task<DataRecord?> FindByKeyAsync(Guid dataSetId, string recordKey, CancellationToken cancellationToken = default);
    Task UpsertAsync(DataRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataRecord>> ListByDataSetAsync(Guid dataSetId, CancellationToken cancellationToken = default);
}
