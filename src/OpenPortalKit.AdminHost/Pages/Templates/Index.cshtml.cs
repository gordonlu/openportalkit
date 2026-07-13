using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.Modules.Content.BlockTemplates;

namespace OpenPortalKit.AdminHost.Pages.Templates;

public sealed class IndexModel : PageModel
{
    private static readonly Guid DevelopmentActorId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid DevelopmentSiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IPageTemplateStore _templateStore;
    private readonly IPageStore _pageStore;
    private readonly PageTemplateService _templateService;
    private readonly PortalPageService _pageService;
    private readonly IBlockDefinitionCatalog _blockCatalog;

    public IndexModel(
        IPageTemplateStore templateStore,
        IPageStore pageStore,
        PageTemplateService templateService,
        PortalPageService pageService,
        IBlockDefinitionCatalog blockCatalog)
    {
        _templateStore = templateStore;
        _pageStore = pageStore;
        _templateService = templateService;
        _pageService = pageService;
        _blockCatalog = blockCatalog;
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
        if (string.IsNullOrWhiteSpace(Template.Code) || string.IsNullOrWhiteSpace(Template.Name))
        {
            ModelState.AddModelError(string.Empty, "Template code and name are required.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var result = await _templateService.SaveAsync(new PageTemplate(
            Guid.NewGuid(),
            Template.Code,
            Template.Name,
            string.IsNullOrWhiteSpace(Template.Description) ? "Reusable server-rendered page template." : Template.Description,
            Template.Publish ? PageTemplateStatus.Published : PageTemplateStatus.Draft,
            1,
            PageTemplateSeedCatalog.CreateDefaultBlocks(Template.SelectedBlockCodes),
            DevelopmentActorId,
            DevelopmentActorId,
            now,
            now), DevelopmentActorId, cancellationToken);

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
        var existingCodes = (await _templateStore.ListAsync(cancellationToken))
            .Select(template => template.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        foreach (var template in PageTemplateSeedCatalog.CreateInitialTemplates(DevelopmentActorId, now)
                     .Where(template => !existingCodes.Contains(template.Code)))
        {
            await _templateService.SaveAsync(template, DevelopmentActorId, cancellationToken);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreatePageAsync(CancellationToken cancellationToken)
    {
        var result = await _pageService.CreateFromTemplateAsync(new CreatePageFromTemplateRequest(
            DevelopmentSiteId,
            NewPage.TemplateCode,
            NewPage.Title,
            NewPage.Slug,
            NewPage.Summary,
            DevelopmentActorId), cancellationToken);
        if (result.Succeeded && NewPage.PublishImmediately)
        {
            result = await _pageService.PublishAsync(
                DevelopmentSiteId,
                result.Page!.Slug,
                DevelopmentActorId,
                cancellationToken);
        }

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
        public bool PublishImmediately { get; set; }
    }
}
