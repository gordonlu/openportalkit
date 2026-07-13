using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.AdminHost.Pages.IndustryPacks;

public sealed class IndexModel : PageModel
{
    private static readonly Guid DevelopmentActorId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private readonly IndustryPackCatalogResult _catalog;
    private readonly IIndustryPackInstallationStore _installationStore;
    private readonly IndustryPackInstallationService _installationService;

    public IndexModel(
        IndustryPackCatalogResult catalog,
        IIndustryPackInstallationStore installationStore,
        IndustryPackInstallationService installationService)
    {
        _catalog = catalog;
        _installationStore = installationStore;
        _installationService = installationService;
    }

    [BindProperty]
    public string PackName { get; set; } = string.Empty;

    public IReadOnlyList<PackView> Packs { get; private set; } = Array.Empty<PackView>();
    public IndustryPackRegistrationPlan? SelectedPlan { get; private set; }

    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostPlanAsync(CancellationToken cancellationToken)
    {
        var pack = FindPack();
        if (pack is null) return NotFound();
        SelectedPlan = await _installationService.PlanAsync(pack, cancellationToken);
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostEnableAsync(CancellationToken cancellationToken)
    {
        var pack = FindPack();
        if (pack is null) return NotFound();
        var result = await _installationService.EnableAsync(pack, DevelopmentActorId, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            SelectedPlan = result.Plan;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDisableAsync(CancellationToken cancellationToken)
    {
        var pack = FindPack();
        if (pack is null) return NotFound();
        var result = await _installationService.DisableAsync(pack, DevelopmentActorId, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    private LoadedIndustryPack? FindPack() => _catalog.Packs.FirstOrDefault(pack =>
        string.Equals(pack.Manifest.Name, PackName, StringComparison.OrdinalIgnoreCase));

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var installations = (await _installationStore.ListAsync(cancellationToken))
            .ToDictionary(item => item.PackName, StringComparer.OrdinalIgnoreCase);
        Packs = _catalog.Packs
            .OrderBy(pack => pack.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(pack => new PackView(pack, installations.GetValueOrDefault(pack.Manifest.Name)))
            .ToArray();
    }

    public sealed record PackView(LoadedIndustryPack Pack, IndustryPackInstallation? Installation)
    {
        public bool IsEnabled => Installation?.IsEnabled ?? false;
    }
}
