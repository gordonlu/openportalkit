using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using OpenPortalKit.AdminHost.Security;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Workflow.Publishing;
using System.Globalization;
using System.Text.Json;

namespace OpenPortalKit.AdminHost.Pages.Templates;

public sealed class PageEditModel : PageModel
{
    private static readonly Guid DevelopmentSiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IPageStore _pageStore;
    private readonly PortalPageService _pageService;
    private readonly IBlockDefinitionCatalog _blockCatalog;
    private readonly ServerRenderedBlockPageRenderer _renderer;
    private readonly IAuthorizationService _authorization;
    private readonly AdminActorContext _actorContext;
    private readonly IPublishingWorkflowItemStore _workflowStore;
    private readonly IApprovalRecordStore _approvalStore;
    private readonly PublishingWorkflowService _workflowService;

    public PageEditModel(
        IPageStore pageStore,
        PortalPageService pageService,
        IBlockDefinitionCatalog blockCatalog,
        ServerRenderedBlockPageRenderer renderer,
        IAuthorizationService authorization,
        AdminActorContext actorContext,
        IPublishingWorkflowItemStore workflowStore,
        IApprovalRecordStore approvalStore,
        PublishingWorkflowService workflowService)
    {
        _pageStore = pageStore;
        _pageService = pageService;
        _blockCatalog = blockCatalog;
        _renderer = renderer;
        _authorization = authorization;
        _actorContext = actorContext;
        _workflowStore = workflowStore;
        _approvalStore = approvalStore;
        _workflowService = workflowService;
    }

    [BindProperty]
    public PageEditorInput Editor { get; set; } = new();

    [BindProperty]
    public string? NewBlockCode { get; set; }

    [BindProperty]
    public int StructuredBlockIndex { get; set; }

    [BindProperty]
    public int StructuredSettingIndex { get; set; }

    [BindProperty]
    public ReviewInput Review { get; set; } = new();

    public IReadOnlyList<BlockDefinition> Definitions { get; private set; } = Array.Empty<BlockDefinition>();
    public IReadOnlyList<PortalPageVersion> Versions { get; private set; } = Array.Empty<PortalPageVersion>();
    public string? PreviewHtml { get; private set; }
    public string? PreviewError { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanPublish { get; private set; }
    public PortalPage? ConflictLatestPage { get; private set; }
    public PublishingWorkflowItem? Workflow { get; private set; }
    public IReadOnlyList<ApprovalRecord> ApprovalHistory { get; private set; } = Array.Empty<ApprovalRecord>();

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var page = await _pageStore.FindBySlugAsync(DevelopmentSiteId, slug, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        Editor = PageEditorInput.FromPage(page, _blockCatalog);
        await LoadChromeAsync(page, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var current = await FindExistingAsync(cancellationToken);
        if (current is null) return NotFound();
        if (!await CanModifyAsync(current, cancellationToken)) return Forbid();
        var result = await SaveAsync(cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            var existing = await FindExistingAsync(cancellationToken);
            if (existing is null) return NotFound();
            if (result.Errors.Contains(PortalPageService.RevisionConflictMessage, StringComparer.Ordinal))
                ConflictLatestPage = existing;
            await LoadChromeAsync(existing, cancellationToken);
            return Page();
        }

        return RedirectToPage(new { slug = result.Page!.Slug });
    }

    public async Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken)
    {
        if (!await CanAsync(AdminAuthorizationPolicies.ContentPublish)) return Forbid();
        var page = await FindExistingAsync(cancellationToken);
        if (page is null) return NotFound();
        var readinessErrors = _pageService.ValidateDraft(page);
        if (readinessErrors.Count > 0)
        {
            AddErrors(readinessErrors);
            await LoadChromeAsync(page, cancellationToken);
            return Page();
        }

        var transitioned = await TransitionAsync(page, WorkflowAction.Publish, cancellationToken);
        if (!transitioned.Succeeded)
        {
            AddErrors(transitioned.Errors);
            await LoadChromeAsync(page, cancellationToken);
            return Page();
        }
        var published = await _pageService.PublishAsync(
            DevelopmentSiteId,
            page.Slug,
            _actorContext.GetActorId(User),
            cancellationToken);
        if (published.Succeeded) return RedirectToPage(new { slug = published.Page!.Slug });
        AddErrors(published.Errors);
        await LoadChromeAsync(page, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitForReviewAsync(CancellationToken cancellationToken)
    {
        var current = await FindExistingAsync(cancellationToken);
        if (current is null) return NotFound();
        if (!await CanModifyAsync(current, cancellationToken)) return Forbid();
        var saved = await SaveAsync(cancellationToken);
        if (!saved.Succeeded)
        {
            AddErrors(saved.Errors);
            await LoadChromeAsync(current, cancellationToken);
            return Page();
        }

        return await CompleteTransitionAsync(saved.Page!, WorkflowAction.SubmitForReview, cancellationToken);
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken cancellationToken) =>
        await ReviewTransitionAsync(WorkflowAction.Approve, cancellationToken);

    public async Task<IActionResult> OnPostRequestChangesAsync(CancellationToken cancellationToken) =>
        await ReviewTransitionAsync(WorkflowAction.RequestChanges, cancellationToken);

    public async Task<IActionResult> OnPostRejectAsync(CancellationToken cancellationToken) =>
        await ReviewTransitionAsync(WorkflowAction.Reject, cancellationToken);

    public async Task<IActionResult> OnPostSchedulePublishAsync(CancellationToken cancellationToken)
    {
        if (!await CanAsync(AdminAuthorizationPolicies.ContentPublish)) return Forbid();
        var page = await FindExistingAsync(cancellationToken);
        if (page is null) return NotFound();
        if (!DateTimeOffset.TryParseExact(
            Review.ScheduledAtUtc,
            "yyyy-MM-dd'T'HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var scheduledAt))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid UTC publication date and time.");
            await LoadChromeAsync(page, cancellationToken);
            return Page();
        }

        var result = await TransitionAsync(page, WorkflowAction.SchedulePublish, cancellationToken, scheduledAt);
        if (result.Succeeded) return RedirectToPage(new { slug = page.Slug });
        AddErrors(result.Errors);
        await LoadChromeAsync(page, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(cancellationToken);
        if (existing is null) return NotFound();
        TrackConflict(existing);
        var candidate = BuildCandidate(existing);
        var validationErrors = _pageService.ValidateDraft(candidate);
        if (validationErrors.Count > 0)
        {
            AddErrors(validationErrors);
            PreviewError = "Preview is unavailable until the validation errors are resolved.";
            await LoadChromeAsync(existing, cancellationToken);
            return Page();
        }
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
        if (!await CanModifyAsync(existing, cancellationToken)) return Forbid();
        TrackConflict(existing);
        var definition = string.IsNullOrWhiteSpace(NewBlockCode)
            ? null
            : _blockCatalog.FindByCode(NewBlockCode);
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
            Editor.Blocks.Add(BlockEditorInput.FromBlock(block, definition));
        }

        await LoadChromeAsync(existing, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddStructuredItemAsync(CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(cancellationToken);
        if (existing is null) return NotFound();
        if (!await CanModifyAsync(existing, cancellationToken)) return Forbid();
        TrackConflict(existing);
        if (StructuredBlockIndex < 0 || StructuredBlockIndex >= Editor.Blocks.Count ||
            StructuredSettingIndex < 0 ||
            StructuredSettingIndex >= Editor.Blocks[StructuredBlockIndex].Settings.Count)
        {
            ModelState.AddModelError(string.Empty, "The structured list target is no longer available.");
        }
        else
        {
            var block = Editor.Blocks[StructuredBlockIndex];
            var definition = _blockCatalog.FindByCode(block.DefinitionCode);
            var setting = definition?.Settings.ElementAtOrDefault(StructuredSettingIndex);
            if (setting?.Type != BlockSettingType.StructuredList)
                ModelState.AddModelError(string.Empty, "The selected setting is not a structured list.");
            else if (block.Settings[StructuredSettingIndex].Items.Count >= 50)
                ModelState.AddModelError(string.Empty, "A structured list can contain at most 50 items.");
            else
                block.Settings[StructuredSettingIndex].Items.Add(new StructuredItemInput());
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
            Editor.Revision,
            _actorContext.GetActorId(User)), cancellationToken);
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
                .Select((block, index) =>
                {
                    var definition = _blockCatalog.FindByCode(block.DefinitionCode);
                    return new BlockInstance(
                        block.Id == Guid.Empty ? Guid.NewGuid() : block.Id,
                        block.DefinitionCode,
                        block.SchemaVersion,
                        index,
                        block.UseExpertJson || definition is null
                            ? block.ConfigurationJson
                            : block.BuildConfigurationJson(definition));
                })
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
        Workflow = await GetOrCreateWorkflowAsync(page, cancellationToken);
        ApprovalHistory = await _approvalStore.FindByTargetAsync(
            "PortalPage", page.Id.ToString("D"), cancellationToken);
        CanEdit = await CanAsync(AdminAuthorizationPolicies.ContentEdit) &&
            Workflow.State is WorkflowState.Draft or WorkflowState.Rejected;
        CanPublish = await CanAsync(AdminAuthorizationPolicies.ContentPublish);
    }

    private async Task<IActionResult> ReviewTransitionAsync(
        WorkflowAction action,
        CancellationToken cancellationToken)
    {
        if (!await CanAsync(AdminAuthorizationPolicies.ContentPublish)) return Forbid();
        var page = await FindExistingAsync(cancellationToken);
        if (page is null) return NotFound();
        return await CompleteTransitionAsync(page, action, cancellationToken);
    }

    private async Task<IActionResult> CompleteTransitionAsync(
        PortalPage page,
        WorkflowAction action,
        CancellationToken cancellationToken)
    {
        var result = await TransitionAsync(page, action, cancellationToken);
        if (result.Succeeded) return RedirectToPage(new { slug = page.Slug });
        AddErrors(result.Errors);
        await LoadChromeAsync(page, cancellationToken);
        return Page();
    }

    private async Task<WorkflowTransitionResult> TransitionAsync(
        PortalPage page,
        WorkflowAction action,
        CancellationToken cancellationToken,
        DateTimeOffset? scheduledAt = null)
    {
        var workflow = await GetOrCreateWorkflowAsync(page, cancellationToken);
        return await _workflowService.TransitionAsync(workflow, new WorkflowTransitionRequest(
            action,
            _actorContext.GetActorId(User),
            string.IsNullOrWhiteSpace(Review.Comment) ? null : Review.Comment.Trim(),
            scheduledAt,
            DateTimeOffset.UtcNow,
            action is WorkflowAction.Publish or WorkflowAction.SchedulePublish
                ? new WorkflowPublicationReadiness(
                    !string.IsNullOrWhiteSpace(page.Title),
                    !string.IsNullOrWhiteSpace(page.Slug),
                    !string.IsNullOrWhiteSpace(page.Summary))
                : null), cancellationToken);
    }

    private async Task<PublishingWorkflowItem> GetOrCreateWorkflowAsync(
        PortalPage page,
        CancellationToken cancellationToken)
    {
        var targetId = page.Id.ToString("D");
        var existing = await _workflowStore.FindByTargetAsync("PortalPage", targetId, cancellationToken);
        if (existing is not null) return existing;
        var state = page.Status switch
        {
            PortalPageStatus.Published => WorkflowState.Published,
            PortalPageStatus.Archived => WorkflowState.Archived,
            _ => WorkflowState.Draft
        };
        var created = new PublishingWorkflowItem(
            Guid.NewGuid(), "PortalPage", targetId, state, 1,
            page.CreatedBy, page.UpdatedBy, page.CreatedAt, page.UpdatedAt,
            PublishedAt: page.PublishedAt,
            ArchivedAt: page.Status == PortalPageStatus.Archived ? page.UpdatedAt : null);
        try
        {
            await _workflowStore.AddAsync(created, cancellationToken);
            return created;
        }
        catch (InvalidOperationException)
        {
            var concurrent = await _workflowStore.FindByTargetAsync("PortalPage", targetId, cancellationToken);
            if (concurrent is null) throw;
            return concurrent;
        }
    }

    private async Task<bool> CanModifyAsync(PortalPage page, CancellationToken cancellationToken)
    {
        if (!await CanAsync(AdminAuthorizationPolicies.ContentEdit)) return false;
        var workflow = await GetOrCreateWorkflowAsync(page, cancellationToken);
        return workflow.State is WorkflowState.Draft or WorkflowState.Rejected;
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
    }

    private async Task<bool> CanAsync(string policy) =>
        (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

    private void TrackConflict(PortalPage current)
    {
        if (Editor.Revision > 0 && Editor.Revision != current.Revision)
            ConflictLatestPage = current;
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

        public static PageEditorInput FromPage(PortalPage page, IBlockDefinitionCatalog catalog) => new()
        {
            CurrentSlug = page.Slug,
            Title = page.Title,
            Slug = page.Slug,
            Summary = page.Summary,
            Status = page.Status,
            Revision = page.Revision,
            Blocks = page.Blocks.OrderBy(block => block.SortOrder)
                .Select(block => BlockEditorInput.FromBlock(block, catalog.FindByCode(block.DefinitionCode)))
                .ToList()
        };
    }

    public sealed class ReviewInput
    {
        public string? Comment { get; set; }
        public string? ScheduledAtUtc { get; set; }
    }

    public sealed class BlockEditorInput
    {
        public Guid Id { get; set; }
        public string DefinitionCode { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string ConfigurationJson { get; set; } = "{}";
        public bool UseExpertJson { get; set; }
        public bool Remove { get; set; }
        public List<BlockSettingInput> Settings { get; set; } = new();

        public static BlockEditorInput FromBlock(BlockInstance block, BlockDefinition? definition) => new()
        {
            Id = block.Id,
            DefinitionCode = block.DefinitionCode,
            SchemaVersion = block.SchemaVersion,
            SortOrder = block.SortOrder,
            ConfigurationJson = block.ConfigurationJson,
            Settings = CreateSettings(block.ConfigurationJson, definition)
        };

        public string BuildConfigurationJson(BlockDefinition definition)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var setting in definition.Settings)
            {
                var input = Settings.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, setting.Key, StringComparison.OrdinalIgnoreCase));
                if (input is null) continue;
                if (setting.Type == BlockSettingType.Boolean)
                {
                    values[setting.Key] = input.BooleanValue;
                    continue;
                }
                var value = input.Value?.Trim() ?? string.Empty;
                if (value.Length == 0 &&
                    (setting.Type != BlockSettingType.StructuredList || input.Items.Count == 0)) continue;
                values[setting.Key] = setting.Type switch
                {
                    BlockSettingType.Number when decimal.TryParse(
                        value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number,
                    BlockSettingType.StructuredList when input.Items.Count > 0 =>
                        input.BuildStructuredItems(definition.Code),
                    BlockSettingType.StructuredList when TryParseJson(value, out var json) => json,
                    _ => value
                };
            }
            return JsonSerializer.Serialize(values);
        }

        private static List<BlockSettingInput> CreateSettings(string configurationJson, BlockDefinition? definition)
        {
            if (definition is null) return new List<BlockSettingInput>();
            try
            {
                using var document = JsonDocument.Parse(configurationJson);
                return definition.Settings.Select(setting =>
                {
                    var hasValue = document.RootElement.TryGetProperty(setting.Key, out var value);
                    return new BlockSettingInput
                    {
                        Key = setting.Key,
                        Type = setting.Type,
                        Value = !hasValue ? string.Empty : setting.Type switch
                        {
                            BlockSettingType.Boolean => string.Empty,
                            BlockSettingType.StructuredList => JsonSerializer.Serialize(
                                value, new JsonSerializerOptions { WriteIndented = true }),
                            BlockSettingType.Number => value.GetRawText(),
                            _ => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
                        },
                        BooleanValue = hasValue && value.ValueKind == JsonValueKind.True,
                        Items = hasValue && setting.Type == BlockSettingType.StructuredList
                            ? StructuredItemInput.FromJson(value)
                            : new List<StructuredItemInput>()
                    };
                }).ToList();
            }
            catch (JsonException)
            {
                return definition.Settings.Select(setting => new BlockSettingInput
                {
                    Key = setting.Key,
                    Type = setting.Type
                }).ToList();
            }
        }

        private static bool TryParseJson(string value, out JsonElement element)
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                element = document.RootElement.Clone();
                return true;
            }
            catch (JsonException)
            {
                element = default;
                return false;
            }
        }
    }

    public sealed class BlockSettingInput
    {
        public string Key { get; set; } = string.Empty;
        public BlockSettingType Type { get; set; }
        public string? Value { get; set; }
        public bool BooleanValue { get; set; }
        public List<StructuredItemInput> Items { get; set; } = new();

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> BuildStructuredItems(string definitionCode)
        {
            return Items.Where(item => !item.Remove)
                .Select(item => item.ToFields(definitionCode))
                .ToArray();
        }
    }

    public sealed class StructuredItemInput
    {
        public string? Label { get; set; }
        public string? Value { get; set; }
        public string? Url { get; set; }
        public string? Description { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public bool Remove { get; set; }

        public IReadOnlyDictionary<string, object?> ToFields(string definitionCode)
        {
            return definitionCode switch
            {
                "chart" => new Dictionary<string, object?>
                {
                    ["label"] = Normalize(Label),
                    ["value"] = decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                        ? number
                        : Normalize(Value)
                },
                "link-list" => new Dictionary<string, object?>
                {
                    ["label"] = Normalize(Label),
                    ["url"] = Normalize(Url)
                },
                "download-list" => new Dictionary<string, object?>
                {
                    ["label"] = Normalize(Label),
                    ["url"] = Normalize(Url),
                    ["description"] = Normalize(Description)
                },
                "faq" => new Dictionary<string, object?>
                {
                    ["question"] = Normalize(Question),
                    ["answer"] = Normalize(Answer)
                },
                _ => new Dictionary<string, object?>()
            };
        }

        public static List<StructuredItemInput> FromJson(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array) return new List<StructuredItemInput>();
            return value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(item => new StructuredItemInput
            {
                Label = GetText(item, "label"),
                Value = item.TryGetProperty("value", out var number) ? number.GetRawText() : string.Empty,
                Url = GetText(item, "url"),
                Description = GetText(item, "description"),
                Question = GetText(item, "question"),
                Answer = GetText(item, "answer")
            }).ToList();
        }

        private static string GetText(JsonElement item, string property) =>
            item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
    }
}
