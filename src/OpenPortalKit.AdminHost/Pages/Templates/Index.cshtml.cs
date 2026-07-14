using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.AdminHost.Security;
using OpenPortalKit.Modules.Content.BlockTemplates;

namespace OpenPortalKit.AdminHost.Pages.Templates;

public sealed class IndexModel : PageModel
{
    private static readonly Guid DevelopmentSiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IPageTemplateStore _templateStore;
    private readonly IPageStore _pageStore;
    private readonly PageTemplateService _templateService;
    private readonly PortalPageService _pageService;
    private readonly IBlockDefinitionCatalog _blockCatalog;
    private readonly IAuthorizationService _authorization;
    private readonly AdminActorContext _actorContext;

    public IndexModel(
        IPageTemplateStore templateStore,
        IPageStore pageStore,
        PageTemplateService templateService,
        PortalPageService pageService,
        IBlockDefinitionCatalog blockCatalog,
        IAuthorizationService authorization,
        AdminActorContext actorContext)
    {
        _templateStore = templateStore;
        _pageStore = pageStore;
        _templateService = templateService;
        _pageService = pageService;
        _blockCatalog = blockCatalog;
        _authorization = authorization;
        _actorContext = actorContext;
    }

    public IReadOnlyList<PageTemplate> Templates { get; private set; } = Array.Empty<PageTemplate>();
    public IReadOnlyList<PortalPage> Pages { get; private set; } = Array.Empty<PortalPage>();
    public IReadOnlyList<BlockDefinition> Blocks { get; private set; } = Array.Empty<BlockDefinition>();

    [BindProperty]
    public TemplateInput Template { get; set; } = new();

    [BindProperty]
    public PageInput NewPage { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateTemplateAsync(CancellationToken cancellationToken)
    {
        if (!await CanEditAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(Template.Code) || string.IsNullOrWhiteSpace(Template.Name))
        {
            ModelState.AddModelError(string.Empty, "Template code and name are required.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var actorId = _actorContext.GetActorId(User);
        var result = await _templateService.SaveAsync(new PageTemplate(
            Guid.NewGuid(),
            Template.Code,
            Template.Name,
            string.IsNullOrWhiteSpace(Template.Description) ? "Reusable server-rendered page template." : Template.Description,
            Template.Publish ? PageTemplateStatus.Published : PageTemplateStatus.Draft,
            1,
            PageTemplateSeedCatalog.CreateDefaultBlocks(Template.SelectedBlockCodes),
            actorId,
            actorId,
            now,
            now), actorId, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSeedTemplatesAsync(CancellationToken cancellationToken)
    {
        if (!await CanEditAsync()) return Forbid();
        var existingCodes = (await _templateStore.ListAsync(cancellationToken))
            .Select(template => template.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var actorId = _actorContext.GetActorId(User);
        foreach (var template in PageTemplateSeedCatalog.CreateInitialTemplates(actorId, now)
                     .Where(template => !existingCodes.Contains(template.Code)))
        {
            await _templateService.SaveAsync(template, actorId, cancellationToken);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreatePageAsync(CancellationToken cancellationToken)
    {
        if (!await CanEditAsync()) return Forbid();
        var actorId = _actorContext.GetActorId(User);
        var result = await _pageService.CreateFromTemplateAsync(new CreatePageFromTemplateRequest(
            DevelopmentSiteId,
            NewPage.TemplateCode,
            NewPage.Title,
            NewPage.Slug,
            NewPage.Summary,
            actorId), cancellationToken);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Templates = await _templateStore.ListAsync(cancellationToken);
        Pages = await _pageStore.ListAsync(DevelopmentSiteId, cancellationToken);
        Blocks = _blockCatalog.List();
    }

    private async Task<bool> CanEditAsync() =>
        (await _authorization.AuthorizeAsync(User, AdminAuthorizationPolicies.ContentEdit)).Succeeded;

    public sealed class TemplateInput
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Publish { get; set; } = true;
        public IReadOnlyList<string> SelectedBlockCodes { get; set; } = new[] { "hero", "rich-text" };
    }

    public sealed class PageInput
    {
        public string TemplateCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
