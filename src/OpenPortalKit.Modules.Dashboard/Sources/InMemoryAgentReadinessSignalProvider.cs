namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed class InMemoryAgentReadinessSignalProvider : IAgentReadinessSignalProvider
{
    private readonly object _gate = new();
    private readonly List<AgentReadinessPageSignal> _signals = new();

    public Task<IReadOnlyList<AgentReadinessPageSignal>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AgentReadinessPageSignal>>(_signals.ToArray());
        }
    }

    public Task ReplaceAsync(
        IEnumerable<AgentReadinessPageSignal> signals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signals);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _signals.Clear();
            _signals.AddRange(signals);
        }

        return Task.CompletedTask;
    }
}
