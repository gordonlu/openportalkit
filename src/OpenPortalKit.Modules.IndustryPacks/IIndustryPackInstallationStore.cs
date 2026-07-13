namespace OpenPortalKit.Modules.IndustryPacks;

public interface IIndustryPackInstallationStore
{
    Task<IReadOnlyList<IndustryPackInstallation>> ListAsync(CancellationToken cancellationToken = default);
    Task<IndustryPackInstallation?> FindAsync(string packName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndustryPackResourceRegistration>> ListResourcesAsync(
        string packName,
        CancellationToken cancellationToken = default);
    Task SaveAsync(
        IndustryPackInstallation installation,
        IReadOnlyList<IndustryPackResourceRegistration> resources,
        CancellationToken cancellationToken = default);
}
