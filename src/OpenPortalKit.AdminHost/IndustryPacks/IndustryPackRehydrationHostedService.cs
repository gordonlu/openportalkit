using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.AdminHost.IndustryPacks;

internal sealed class IndustryPackRehydrationHostedService : IHostedService
{
    private readonly IndustryPackInstallationService _installationService;
    private readonly IndustryPackCatalogResult _catalog;
    private readonly ILogger<IndustryPackRehydrationHostedService> _logger;

    public IndustryPackRehydrationHostedService(
        IndustryPackInstallationService installationService,
        IndustryPackCatalogResult catalog,
        ILogger<IndustryPackRehydrationHostedService> logger)
    {
        _installationService = installationService;
        _catalog = catalog;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await _installationService.RehydrateEnabledAsync(_catalog.Packs, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Industry pack runtime rehydration failed: " + string.Join("; ", result.Errors));
        }

        _logger.LogInformation(
            "Rehydrated {PackCount} enabled industry packs and {ResourceCount} runtime resources.",
            result.RehydratedPackCount,
            result.RehydratedResourceCount);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
