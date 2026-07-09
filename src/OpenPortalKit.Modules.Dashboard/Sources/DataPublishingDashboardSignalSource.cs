using OpenPortalKit.Modules.Dashboard.Analytics;
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
    private readonly IAnalyticsEventStore? _eventStore;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Guid? _siteId;
    private readonly int _staleAfterDays;
    private readonly int _analyticsLookbackDays;

    public DataPublishingDashboardSignalSource(
        IDataSetStore dataSetStore,
        IDataRecordStore recordStore,
        Guid? siteId = null,
        int staleAfterDays = 30,
        Func<DateTimeOffset>? clock = null,
        IAnalyticsEventStore? eventStore = null,
        int analyticsLookbackDays = 7)
    {
        _dataSetStore = dataSetStore ?? throw new ArgumentNullException(nameof(dataSetStore));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _eventStore = eventStore;
        _siteId = siteId;
        _staleAfterDays = staleAfterDays;
        _analyticsLookbackDays = analyticsLookbackDays;
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
        var batchIds = new HashSet<Guid>();
        var latestRecordUpdatedAt = DateTimeOffset.MinValue;
        var topDatasetRecordCount = 0;

        foreach (var dataSet in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var records = await _recordStore.ListByDataSetAsync(dataSet.Id, cancellationToken);
            recordCount += records.Count;
            missingSourceCount += records.Count(record => string.IsNullOrWhiteSpace(record.Source));
            topDatasetRecordCount = Math.Max(topDatasetRecordCount, records.Count);

            foreach (var record in records)
            {
                batchIds.Add(record.SourceBatchId);
                if (record.UpdatedAt > latestRecordUpdatedAt)
                {
                    latestRecordUpdatedAt = record.UpdatedAt;
                }
            }

            if (dataSet.IsPublic && (records.Count == 0 || records.Max(record => record.AsOfDate) < staleBefore))
            {
                staleDataSetCount++;
            }
        }

        var events = await ListAnalyticsEventsAsync(observedAt, cancellationToken);
        var dataImportEvents = events.Where(item => IsEvent(item, "data_import")).ToArray();
        var importFailureCount = dataImportEvents.Count(IsFailedStatus);
        var importSuccessCount = Math.Max(
            batchIds.Count,
            dataImportEvents.Count(IsSuccessStatus));
        var qualityFailureCount = dataImportEvents.Count(item =>
            item.Metadata.TryGetValue("quality_status", out var qualityStatus) &&
            string.Equals(qualityStatus, "failed", StringComparison.OrdinalIgnoreCase));
        var datasetApiRequestCount = events.Count(IsDatasetApiRequest);
        var datasetExportCount = events.Count(IsDatasetExport);
        var topDatasetRequestCount = MaxGroupCount(
            events.Where(item => IsDatasetApiRequest(item) || IsDatasetExport(item)),
            DatasetCode);
        var latestSnapshotStatus = latestRecordUpdatedAt == DateTimeOffset.MinValue
            ? 0
            : latestRecordUpdatedAt >= observedAt.AddDays(-_staleAfterDays)
                ? 1
                : 0;

        var metrics = new[]
        {
            Metric("data.datasetCount", "Datasets", dataSets.Count, "sets", observedAt, 10),
            Metric("data.publicDatasetCount", "Public datasets", dataSets.Count(dataSet => dataSet.IsPublic), "sets", observedAt, 20),
            Metric("data.recordCount", "Records", recordCount, "records", observedAt, 30),
            Metric("data.importBatchCount", "Import batches", batchIds.Count, "batches", observedAt, 40),
            Metric("data.importSuccessCount", "Import successes", importSuccessCount, "imports", observedAt, 50),
            Metric("data.importFailureCount", "Import failures", importFailureCount, "imports", observedAt, 60),
            Metric("data.qualityFailureCount", "Quality failures", qualityFailureCount, "imports", observedAt, 70),
            Metric("data.staleDatasetCount", "Stale datasets", staleDataSetCount, "sets", observedAt, 80),
            Metric("data.missingSourceCount", "Missing source", missingSourceCount, "records", observedAt, 90),
            Metric("data.missingAsOfDateCount", "Missing as-of date", 0, "records", observedAt, 100),
            Metric("data.latestSnapshotStatus", "Latest snapshot", latestSnapshotStatus, "status", observedAt, 110),
            Metric("data.datasetApiRequestCount", "Dataset API requests", datasetApiRequestCount, "requests", observedAt, 120),
            Metric("data.datasetExportCount", "Dataset exports", datasetExportCount, "exports", observedAt, 130),
            Metric("data.topDatasetRecordCount", "Top dataset records", topDatasetRecordCount, "records", observedAt, 140),
            Metric("data.topDatasetRequestCount", "Top dataset requests", topDatasetRequestCount, "requests", observedAt, 150)
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

        if (importFailureCount > 0 || qualityFailureCount > 0)
        {
            alerts.Add(Alert(
                "data.importFailures",
                "Data imports or quality checks have failures.",
                DashboardAlertLevel.Critical,
                observedAt));
        }

        return new DashboardSignalSet(SourceModule, metrics, alerts);
    }

    private async Task<IReadOnlyList<AnalyticsEvent>> ListAnalyticsEventsAsync(
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        if (_eventStore is null)
        {
            return Array.Empty<AnalyticsEvent>();
        }

        return await _eventStore.ListAsync(
            new AnalyticsEventQuery(
                From: observedAt.AddDays(-_analyticsLookbackDays),
                To: observedAt,
                Take: 10000),
            cancellationToken);
    }

    private static bool IsEvent(AnalyticsEvent analyticsEvent, string eventType)
    {
        return string.Equals(analyticsEvent.EventType, eventType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessStatus(AnalyticsEvent analyticsEvent)
    {
        return analyticsEvent.Metadata.TryGetValue("status", out var status) &&
            (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "success", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFailedStatus(AnalyticsEvent analyticsEvent)
    {
        return analyticsEvent.Metadata.TryGetValue("status", out var status) &&
            string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatasetApiRequest(AnalyticsEvent analyticsEvent)
    {
        return IsEvent(analyticsEvent, "api_request") &&
            analyticsEvent.Path.StartsWith("/api/public/datasets", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatasetExport(AnalyticsEvent analyticsEvent)
    {
        return IsEvent(analyticsEvent, "dataset_export") ||
            (IsDatasetApiRequest(analyticsEvent) &&
                analyticsEvent.Path.EndsWith("/export.csv", StringComparison.OrdinalIgnoreCase));
    }

    private static string? DatasetCode(AnalyticsEvent analyticsEvent)
    {
        if (analyticsEvent.Metadata.TryGetValue("dataset_code", out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = analyticsEvent.Path
            .Split('?', 2)[0]
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4 &&
            string.Equals(parts[0], "api", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[1], "public", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[2], "datasets", StringComparison.OrdinalIgnoreCase)
            ? parts[3]
            : null;
    }

    private static int MaxGroupCount(
        IEnumerable<AnalyticsEvent> events,
        Func<AnalyticsEvent, string?> keySelector)
    {
        return events
            .Select(keySelector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count())
            .DefaultIfEmpty(0)
            .Max();
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
