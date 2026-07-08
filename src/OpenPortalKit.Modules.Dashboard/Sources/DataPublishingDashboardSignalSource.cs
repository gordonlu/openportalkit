using OpenPortalKit.Modules.Dashboard.Summaries;
using OpenPortalKit.Modules.Data;
using OpenPortalKit.Modules.Data.Datasets;

namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class DataPublishingDashboardSignalSource : IDashboardSignalSource
{
    private const string CardCode = "data.publishing";
    private const string CardTitle = "Data publishing";
    private readonly IDataSetStore _dataSetStore;
    private readonly IDataRecordStore _recordStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Guid? _siteId;
    private readonly int _staleAfterDays;

    public DataPublishingDashboardSignalSource(
        IDataSetStore dataSetStore,
        IDataRecordStore recordStore,
        Guid? siteId = null,
        int staleAfterDays = 30,
        Func<DateTimeOffset>? clock = null)
    {
        _dataSetStore = dataSetStore ?? throw new ArgumentNullException(nameof(dataSetStore));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _siteId = siteId;
        _staleAfterDays = staleAfterDays;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string SourceModule => DataModule.Descriptor.Name;

    public async Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        var observedAt = _clock();
        var dataSets = await _dataSetStore.ListDataSetsAsync(_siteId, cancellationToken);
        var staleBefore = DateOnly.FromDateTime(observedAt.AddDays(-_staleAfterDays).UtcDateTime);

        var recordCount = 0;
        var staleDataSetCount = 0;
        var missingSourceCount = 0;

        foreach (var dataSet in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var records = await _recordStore.ListByDataSetAsync(dataSet.Id, cancellationToken);
            recordCount += records.Count;
            missingSourceCount += records.Count(record => string.IsNullOrWhiteSpace(record.Source));

            if (dataSet.IsPublic && (records.Count == 0 || records.Max(record => record.AsOfDate) < staleBefore))
            {
                staleDataSetCount++;
            }
        }

        var metrics = new[]
        {
            Metric("data.datasetCount", "Datasets", dataSets.Count, "sets", observedAt, 10),
            Metric("data.publicDatasetCount", "Public datasets", dataSets.Count(dataSet => dataSet.IsPublic), "sets", observedAt, 20),
            Metric("data.recordCount", "Records", recordCount, "records", observedAt, 30),
            Metric("data.staleDatasetCount", "Stale datasets", staleDataSetCount, "sets", observedAt, 40),
            Metric("data.missingSourceCount", "Missing source", missingSourceCount, "records", observedAt, 50)
        };

        var alerts = new List<DashboardAlert>();
        if (staleDataSetCount > 0)
        {
            alerts.Add(Alert(
                "data.stale",
                "Public datasets need a freshness review.",
                DashboardAlertLevel.Warning,
                observedAt));
        }

        if (missingSourceCount > 0)
        {
            alerts.Add(Alert(
                "data.source",
                "Structured records are missing source attribution.",
                DashboardAlertLevel.Critical,
                observedAt));
        }

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private static DashboardMetricSnapshot Metric(
        string code,
        string label,
        decimal value,
        string unit,
        DateTimeOffset observedAt,
        int sortOrder)
    {
        return new DashboardMetricSnapshot(
            code,
            label,
            DashboardArea.DataPublishing,
            CardCode,
            CardTitle,
            value,
            unit,
            observedAt,
            DataModule.Descriptor.Name,
            sortOrder);
    }

    private static DashboardAlert Alert(
        string code,
        string message,
        DashboardAlertLevel level,
        DateTimeOffset observedAt)
    {
        return new DashboardAlert(
            code,
            message,
            DashboardArea.DataPublishing,
            CardCode,
            CardTitle,
            level,
            DataModule.Descriptor.Name,
            observedAt,
            "/Content");
    }
}
