namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public interface IAgentOutputArtifactStore
{
    Task UpsertAsync(AgentOutputArtifact artifact, CancellationToken cancellationToken = default);

    Task<AgentOutputArtifact?> FindByPathAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentOutputArtifact>> ListAsync(CancellationToken cancellationToken = default);
}
