using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.Modules.Content.BlockTemplates;

namespace OpenPortalKit.AdminHost.Pages.Templates;

public sealed class PageEditModel : PageModel
{
    private static readonly Guid DevelopmentActorId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid DevelopmentSiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IPageStore _pageStore;
    private readonly PortalPageService _pageService;
    private readonly IBlockDefinitionCatalog _blockCatalog;
    private readonly ServerRenderedBlockPageRenderer _renderer;

    public PageEditModel(
        IPageStore pageStore,
        PortalPageService pageService,
        IBlockDefinitionCatalog blockCatalog,
        ServerRenderedBlockPageRenderer renderer)
    {
        _pageStore = pageStore;
        _pageService = pageService;
        _blockCatalog = blockCatalog;
        _renderer = renderer;
    }

    [BindProperty]
    public PageEditorInput Editor { get; set; } = new();

    [BindProperty]
    public string NewBlockCode { get; set; } = string.Empty;

    public IReadOnlyList<BlockDefinition> Definitions { get; private set; } = Array.Empty<BlockDefinition>();
    public IReadOnlyList<PortalPageVersion> Versions { get; private set; } = Array.Empty<PortalPageVersion>();
    public string? PreviewHtml { get; private set; }
    public string? PreviewError { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var page = await _pageStore.FindBySlugAsync(DevelopmentSiteId, slug, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        Editor = PageEditorInput.FromPage(page);
        await LoadChromeAsync(page, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var result = await SaveAsync(cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            var existing = await FindExistingAsync(cancellationToken);
            if (existing is null) return NotFound();
            await LoadChromeAsync(existing, cancellationToken);
            return Page();
        }

        return RedirectToPage(new { slug = result.Page!.Slug });
    }

    public async Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken)
    {
        var saved = await SaveAsync(cancellationToken);
        if (!saved.Succeeded)
        {
            AddErrors(saved.Errors);
            var existing = await FindExistingAsync(cancellationToken);
            if (existing is null) return NotFound();
            await LoadChromeAsync(existing, cancellationToken);
            return Page();
        }

        var published = await _pageService.PublishAsync(
            DevelopmentSiteId,
            saved.Page!.Slug,
            DevelopmentActorId,
            cancellationToken);
        return published.Succeeded
            ? RedirectToPage(new { slug = published.Page!.Slug })
            : Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(cancellationToken);
        if (existing is null) return NotFound();
        var candidate = BuildCandidate(existing);
        try
        {
            PreviewHtml = await _renderer.RenderBodyAsync(candidate, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            PreviewError = exception.Message;
        }

        await LoadChromeAsync(existing, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddBlockAsync(CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(cancellationToken);
        if (existing is null) return NotFound();
        var definition = _blockCatalog.FindByCode(NewBlockCode);
        if (definition is null)
        {
            ModelState.AddModelError(string.Empty, "Select a predefined block to add.");
        }
        else
        {
            var block = PageTemplateSeedCatalog.CreateDefaultBlocks(new[] { definition.Code })[0] with
            {
                SortOrder = Editor.Blocks.Count
            };
            Editor.Blocks.Add(BlockEditorInput.FromBlock(block));
        }

        await LoadChromeAsync(existing, cancellationToken);
        return Page();
    }

    private async Task<PortalPageOperationResult> SaveAsync(CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(cancellationToken);
        if (existing is null)
        {
            return new PortalPageOperationResult(false, null, new[] { "Page was not found." });
        }

        var candidate = BuildCandidate(existing);
        return await _pageService.UpdateAsync(new UpdatePortalPageRequest(
            DevelopmentSiteId,
            Editor.CurrentSlug,
            candidate.Title,
            candidate.Slug,
            candidate.Summary,
            candidate.Blocks,
            DevelopmentActorId), cancellationToken);
    }

    private PortalPage BuildCandidate(PortalPage existing)
    {
        return existing with
        {
            Title = Editor.Title.Trim(),
            Slug = Editor.Slug.Trim(),
            Summary = Editor.Summary.Trim(),
            Blocks = Editor.Blocks
                .Where(block => !block.Remove)
                .OrderBy(block => block.SortOrder)
                .ThenBy(block => block.Id)
                .Select((block, index) => new BlockInstance(
                    block.Id == Guid.Empty ? Guid.NewGuid() : block.Id,
                    block.DefinitionCode,
                    block.SchemaVersion,
                    index,
                    block.ConfigurationJson))
                .ToArray()
        };
    }

    private Task<PortalPage?> FindExistingAsync(CancellationToken cancellationToken)
    {
        return _pageStore.FindBySlugAsync(DevelopmentSiteId, Editor.CurrentSlug, cancellationToken);
    }

    private async Task LoadChromeAsync(PortalPage page, CancellationToken cancellationToken)
    {
        Definitions = _blockCatalog.List();
        Versions = await _pageStore.ListVersionsAsync(page.Id, cancellationToken);
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
    }

    public sealed class PageEditorInput
    {
        public string CurrentSlug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public PortalPageStatus Status { get; set; }
        public int Revision { get; set; }
        public List<BlockEditorInput> Blocks { get; set; } = new();

        public static PageEditorInput FromPage(PortalPage page) => new()
        {
            CurrentSlug = page.Slug,
            Title = page.Title,
            Slug = page.Slug,
            Summary = page.Summary,
            Status = page.Status,
            Revision = page.Revision,
            Blocks = page.Blocks.OrderBy(block => block.SortOrder).Select(BlockEditorInput.FromBlock).ToList()
        };
    }

    public sealed class BlockEditorInput
    {
        public Guid Id { get; set; }
        public string DefinitionCode { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string ConfigurationJson { get; set; } = "{}";
        public bool Remove { get; set; }

        public static BlockEditorInput FromBlock(BlockInstance block) => new()
        {
            Id = block.Id,
            DefinitionCode = block.DefinitionCode,
            SchemaVersion = block.SchemaVersion,
            SortOrder = block.SortOrder,
            ConfigurationJson = block.ConfigurationJson
        };
    }
}
