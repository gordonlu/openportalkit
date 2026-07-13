namespace OpenPortalKit.Modules.IndustryPacks;

public sealed class InMemoryIndustryPackInstallationStore : IIndustryPackInstallationStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IndustryPackInstallation> _installations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<IndustryPackResourceRegistration>> _resources = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<IndustryPackInstallation>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<IndustryPackInstallation>>(_installations.Values
                .OrderBy(item => item.PackName, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    public Task<IndustryPackInstallation?> FindAsync(string packName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_installations.GetValueOrDefault(packName));
        }
    }

    public Task<IReadOnlyList<IndustryPackResourceRegistration>> ListResourcesAsync(
        string packName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_resources.GetValueOrDefault(packName) ?? Array.Empty<IndustryPackResourceRegistration>());
        }
    }

    public Task SaveAsync(
        IndustryPackInstallation installation,
        IReadOnlyList<IndustryPackResourceRegistration> resources,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _installations[installation.PackName] = installation;
            _resources[installation.PackName] = resources.ToArray();
        }

        return Task.CompletedTask;
    }
}
