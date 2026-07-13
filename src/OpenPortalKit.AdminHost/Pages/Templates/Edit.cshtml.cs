using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.Modules.Content.BlockTemplates;

namespace OpenPortalKit.AdminHost.Pages.Templates;

public sealed class EditModel : PageModel
{
    private static readonly Guid DevelopmentActorId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid DevelopmentSiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IPageTemplateStore _templateStore;
    private readonly PageTemplateService _templateService;
    private readonly IBlockDefinitionCatalog _blockCatalog;
    private readonly ServerRenderedBlockPageRenderer _renderer;

    public EditModel(
        IPageTemplateStore templateStore,
        PageTemplateService templateService,
        IBlockDefinitionCatalog blockCatalog,
        ServerRenderedBlockPageRenderer renderer)
    {
        _templateStore = templateStore;
        _templateService = templateService;
        _blockCatalog = blockCatalog;
        _renderer = renderer;
    }

    [BindProperty]
    public TemplateEditorInput Editor { get; set; } = new();

    [BindProperty]
    public string NewBlockCode { get; set; } = string.Empty;

    public IReadOnlyList<BlockDefinition> Definitions { get; private set; } = Array.Empty<BlockDefinition>();
    public IReadOnlyList<PageTemplateVersion> Versions { get; private set; } = Array.Empty<PageTemplateVersion>();
    public string? PreviewHtml { get; private set; }
    public string? PreviewError { get; private set; }

    public async Task<IActionResult> OnGetAsync(string code, CancellationToken cancellationToken)
    {
        var template = await _templateStore.FindByCodeAsync(code, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        Editor = TemplateEditorInput.FromTemplate(template);
        await LoadChromeAsync(template, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var existing = await _templateStore.FindByCodeAsync(Editor.Code, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var candidate = BuildCandidate(existing);
        var result = await _templateService.SaveAsync(candidate, DevelopmentActorId, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await LoadChromeAsync(existing, cancellationToken);
            return Page();
        }

        return RedirectToPage(new { code = result.Template!.Code });
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        var existing = await _templateStore.FindByCodeAsync(Editor.Code, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var candidate = BuildCandidate(existing);
        var validation = PageTemplateValidator.Validate(candidate, _blockCatalog);
        if (!validation.IsValid)
        {
            AddErrors(validation.Errors);
        }
        else
        {
            try
            {
                PreviewHtml = await _renderer.RenderBodyAsync(new PortalPage(
                    Guid.NewGuid(),
                    DevelopmentSiteId,
                    candidate.Id,
                    candidate.Version,
                    candidate.Name,
                    candidate.Code + "-preview",
                    candidate.Description,
                    PortalPageStatus.Draft,
                    candidate.Blocks,
                    DevelopmentActorId,
                    DevelopmentActorId,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    null), cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                PreviewError = exception.Message;
            }
        }

        await LoadChromeAsync(existing, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddBlockAsync(CancellationToken cancellationToken)
    {
        var existing = await _templateStore.FindByCodeAsync(Editor.Code, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

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

    private PageTemplate BuildCandidate(PageTemplate existing)
    {
        var blocks = Editor.Blocks
            .Where(block => !block.Remove)
            .OrderBy(block => block.SortOrder)
            .ThenBy(block => block.Id)
            .Select((block, index) => new BlockInstance(
                block.Id == Guid.Empty ? Guid.NewGuid() : block.Id,
                block.DefinitionCode,
                block.SchemaVersion,
                index,
                block.ConfigurationJson))
            .ToArray();

        return existing with
        {
            Name = Editor.Name.Trim(),
            Description = Editor.Description.Trim(),
            Status = Editor.Status,
            Blocks = blocks
        };
    }

    private async Task LoadChromeAsync(PageTemplate template, CancellationToken cancellationToken)
    {
        Definitions = _blockCatalog.List();
        Versions = await _templateStore.ListVersionsAsync(template.Id, cancellationToken);
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    public sealed class TemplateEditorInput
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PageTemplateStatus Status { get; set; }
        public List<BlockEditorInput> Blocks { get; set; } = new();

        public static TemplateEditorInput FromTemplate(PageTemplate template)
        {
            return new TemplateEditorInput
            {
                Code = template.Code,
                Name = template.Name,
                Description = template.Description,
                Status = template.Status,
                Blocks = template.Blocks.OrderBy(block => block.SortOrder).Select(BlockEditorInput.FromBlock).ToList()
            };
        }
    }

    public sealed class BlockEditorInput
    {
        public Guid Id { get; set; }
        public string DefinitionCode { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string ConfigurationJson { get; set; } = "{}";
        public bool Remove { get; set; }

        public static BlockEditorInput FromBlock(BlockInstance block)
        {
            return new BlockEditorInput
            {
                Id = block.Id,
                DefinitionCode = block.DefinitionCode,
                SchemaVersion = block.SchemaVersion,
                SortOrder = block.SortOrder,
                ConfigurationJson = block.ConfigurationJson
            };
        }
    }
}
