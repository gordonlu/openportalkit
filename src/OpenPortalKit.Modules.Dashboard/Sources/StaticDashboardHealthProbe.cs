namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class StaticDashboardHealthProbe : IDashboardHealthProbe
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<DashboardHealthProbeResult> _check;

    public string Code { get; }

    public StaticDashboardHealthProbe(
        string code,
        string label,
        DashboardHealthProbeStatus status,
        TimeSpan latency,
        string? message = null,
        string? actionHref = null,
        Func<DateTimeOffset>? clock = null)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Probe code is required.", nameof(code))
            : code;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _check = () => new DashboardHealthProbeResult(
            Code,
            label,
            status,
            latency,
            _clock(),
            message,
            actionHref);
    }

    public StaticDashboardHealthProbe(
        string code,
        Func<DashboardHealthProbeResult> check)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Probe code is required.", nameof(code))
            : code;
        _check = check ?? throw new ArgumentNullException(nameof(check));
        _clock = () => DateTimeOffset.UtcNow;
    }

    public Task<DashboardHealthProbeResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_check());
    }
}
