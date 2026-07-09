namespace OpenPortalKit.Modules.Dashboard.Sources;

public interface IAgentReadinessSignalProvider
{
    Task<IReadOnlyList<AgentReadinessPageSignal>> ListAsync(
        CancellationToken cancellationToken = default);
}
