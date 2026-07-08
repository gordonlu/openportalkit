using System.Diagnostics.Metrics;
using OpenPortalKit.Modules.Dashboard.Summaries;

namespace OpenPortalKit.Modules.Dashboard.Observability;

public sealed class DashboardTelemetryPublisher : IDisposable
{
    public const string MeterName = "OpenPortalKit.Dashboard";

    private readonly Meter _meter;
    private readonly Histogram<double> _metricValues;
    private readonly ObservableGauge<int> _cardCount;
    private readonly ObservableGauge<int> _alertCount;
    private readonly object _gate = new();
    private DashboardSnapshot? _latest;

    public DashboardTelemetryPublisher()
    {
        _meter = new Meter(MeterName);
        _metricValues = _meter.CreateHistogram<double>(
            "openportalkit.dashboard.metric.value",
            description: "Dashboard metric values emitted from the latest summary.");
        _cardCount = _meter.CreateObservableGauge(
            "openportalkit.dashboard.cards",
            ObserveCardCount,
            description: "Number of cards in the latest dashboard summary.");
        _alertCount = _meter.CreateObservableGauge(
            "openportalkit.dashboard.alerts",
            ObserveAlertCount,
            description: "Number of alerts in the latest dashboard summary.");
    }

    public void Publish(DashboardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            _latest = snapshot;
        }

        foreach (var metric in snapshot.Summary.Cards.SelectMany(card => card.Metrics))
        {
            _metricValues.Record(
                (double)metric.Value,
                new KeyValuePair<string, object?>("area", metric.Area.ToString()),
                new KeyValuePair<string, object?>("card", metric.CardCode),
                new KeyValuePair<string, object?>("code", metric.Code),
                new KeyValuePair<string, object?>("unit", metric.Unit),
                new KeyValuePair<string, object?>("source", metric.SourceModule));
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private Measurement<int> ObserveCardCount()
    {
        lock (_gate)
        {
            return new Measurement<int>(_latest?.Summary.Cards.Count ?? 0);
        }
    }

    private Measurement<int> ObserveAlertCount()
    {
        lock (_gate)
        {
            return new Measurement<int>(
                _latest?.Summary.Cards.Sum(card => card.Alerts.Count) + _latest?.Summary.Alerts.Count ?? 0);
        }
    }
}
