using System.Text.Json;
using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Modules.Content.ContentItems;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PageTemplateService
{
    private readonly IPageTemplateStore _store;
    private readonly IBlockDefinitionCatalog _catalog;
    private readonly AuditRecorder _auditRecorder;
    private readonly Func<DateTimeOffset> _clock;

    public PageTemplateService(
        IPageTemplateStore store,
        IBlockDefinitionCatalog catalog,
        AuditRecorder auditRecorder,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _auditRecorder = auditRecorder ?? throw new ArgumentNullException(nameof(auditRecorder));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<PageTemplateSaveResult> SaveAsync(
        PageTemplate template,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (string.IsNullOrWhiteSpace(template.Code))
        {
            return new PageTemplateSaveResult(
                false,
                null,
                new[] { "Template code is required." });
        }

        var normalizedCode = SlugGenerator.Generate(template.Code);
        var candidate = template with { Code = normalizedCode };
        var existing = await _store.FindByCodeAsync(candidate.Code, cancellationToken);
        var versioned = candidate with { Version = existing is null ? 1 : existing.Version + 1 };
        var validation = PageTemplateValidator.Validate(versioned, _catalog);
        if (!validation.IsValid)
        {
            return new PageTemplateSaveResult(false, null, validation.Errors);
        }

        if (existing is not null && existing.Id != versioned.Id)
        {
            return new PageTemplateSaveResult(
                false,
                null,
                new[] { $"Template code '{versioned.Code}' is already in use." });
        }

        var now = _clock();
        var normalized = versioned with
        {
            UpdatedBy = actorId,
            UpdatedAt = now,
            CreatedBy = template.CreatedBy == Guid.Empty ? actorId : template.CreatedBy,
            CreatedAt = template.CreatedAt == default ? now : template.CreatedAt,
            Blocks = template.Blocks.OrderBy(block => block.SortOrder).ToArray()
        };

        await _store.SaveAsync(normalized, cancellationToken);
        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            actorId,
            existing is null ? "block-template.created" : "block-template.updated",
            "PageTemplate",
            normalized.Id.ToString("D"),
            $"Saved block template '{normalized.Name}' version {normalized.Version}.",
            JsonSerializer.Serialize(new
            {
                normalized.Code,
                normalized.Version,
                normalized.Status,
                BlockCodes = normalized.Blocks.Select(block => block.DefinitionCode).ToArray()
            })), cancellationToken);

        return new PageTemplateSaveResult(true, normalized, Array.Empty<string>());
    }

    public Task<PageTemplate?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return _store.FindByCodeAsync(SlugGenerator.Generate(code), cancellationToken);
    }
}
