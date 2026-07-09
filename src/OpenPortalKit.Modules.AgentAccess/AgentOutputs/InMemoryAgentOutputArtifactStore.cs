namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class InMemoryAgentOutputArtifactStore : IAgentOutputArtifactStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentOutputArtifact> _artifacts = new(StringComparer.OrdinalIgnoreCase);

    public Task UpsertAsync(AgentOutputArtifact artifact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifact.Path);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _artifacts[artifact.Path] = artifact;
        }

        return Task.CompletedTask;
    }

    public Task<AgentOutputArtifact?> FindByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_artifacts.GetValueOrDefault(path));
        }
    }

    public Task<IReadOnlyList<AgentOutputArtifact>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AgentOutputArtifact>>(
                _artifacts.Values
                    .OrderBy(artifact => artifact.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
    }
}
