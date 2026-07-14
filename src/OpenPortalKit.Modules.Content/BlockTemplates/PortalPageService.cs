using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Modules.Content.ContentItems;
using System.Text.Json;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PortalPageService
{
    public const string RevisionConflictMessage =
        "This page changed after it was opened. Reload the latest revision before saving.";

    private readonly IPageTemplateStore _templateStore;
    private readonly IPageStore _pageStore;
    private readonly AuditRecorder _auditRecorder;
    private readonly IOutboxMessageStore _outboxMessageStore;
    private readonly IBlockDefinitionCatalog _blockCatalog;
    private readonly Func<DateTimeOffset> _clock;

    public PortalPageService(
        IPageTemplateStore templateStore,
        IPageStore pageStore,
        AuditRecorder auditRecorder,
        IOutboxMessageStore outboxMessageStore,
        IBlockDefinitionCatalog blockCatalog,
        Func<DateTimeOffset>? clock = null)
    {
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _pageStore = pageStore ?? throw new ArgumentNullException(nameof(pageStore));
        _auditRecorder = auditRecorder ?? throw new ArgumentNullException(nameof(auditRecorder));
        _outboxMessageStore = outboxMessageStore ?? throw new ArgumentNullException(nameof(outboxMessageStore));
        _blockCatalog = blockCatalog ?? throw new ArgumentNullException(nameof(blockCatalog));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<PortalPageOperationResult> UpdateAsync(
        UpdatePortalPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return new PortalPageOperationResult(false, null, errors);
        }

        var page = await _pageStore.FindBySlugAsync(
            request.SiteId,
            SlugGenerator.Generate(request.CurrentSlug),
            cancellationToken);
        if (page is null || page.Status == PortalPageStatus.Archived)
        {
            return new PortalPageOperationResult(false, null, new[] { "Page is not available for editing." });
        }

        if (page.Revision != request.ExpectedRevision)
        {
            return RevisionConflict();
        }

        var slug = SlugGenerator.Generate(request.Slug);
        if (page.Status == PortalPageStatus.Published && !string.Equals(page.Slug, slug, StringComparison.Ordinal))
        {
            return new PortalPageOperationResult(false, null, new[] { "A published page slug cannot be changed." });
        }

        var duplicate = await _pageStore.FindBySlugAsync(request.SiteId, slug, cancellationToken);
        if (duplicate is not null && duplicate.Id != page.Id)
        {
            return new PortalPageOperationResult(false, null, new[] { $"Page slug '{slug}' is already in use." });
        }

        var blocks = request.Blocks.OrderBy(block => block.SortOrder).ToArray();
        var now = _clock();
        var updated = page with
        {
            Title = request.Title.Trim(),
            Slug = slug,
            Summary = request.Summary.Trim(),
            Blocks = blocks,
            UpdatedBy = request.ActorId,
            UpdatedAt = now,
            Revision = page.Revision + 1
        };
        var validationErrors = ValidateDraft(updated);
        if (validationErrors.Count > 0)
            return new PortalPageOperationResult(false, null, validationErrors);
        if (!await _pageStore.TryUpdateAsync(updated, request.ExpectedRevision, cancellationToken))
        {
            return RevisionConflict();
        }
        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            request.ActorId,
            "portal-page.updated",
            "PortalPage",
            updated.Id.ToString("D"),
            $"Saved page '{updated.Title}' revision {updated.Revision}.",
            JsonSerializer.Serialize(new
            {
                updated.Slug,
                updated.Revision,
                updated.Status,
                BlockCodes = updated.Blocks.Select(block => block.DefinitionCode).ToArray()
            })), cancellationToken);

        if (updated.Status == PortalPageStatus.Published)
        {
            await EnqueuePublicPageChangeAsync(updated, now, cancellationToken);
        }

        return new PortalPageOperationResult(true, updated, Array.Empty<string>());
    }

    public async Task<PortalPageOperationResult> CreateFromTemplateAsync(
        CreatePageFromTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return new PortalPageOperationResult(false, null, errors);
        }

        var template = await _templateStore.FindByCodeAsync(
            SlugGenerator.Generate(request.TemplateCode),
            cancellationToken);
        if (template is null || template.Status != PageTemplateStatus.Published)
        {
            return new PortalPageOperationResult(
                false,
                null,
                new[] { "A published page template is required." });
        }

        var slug = SlugGenerator.Generate(request.Slug);
        var existing = await _pageStore.FindBySlugAsync(request.SiteId, slug, cancellationToken);
        if (existing is not null)
        {
            return new PortalPageOperationResult(
                false,
                null,
                new[] { $"Page slug '{slug}' is already in use." });
        }

        var now = _clock();
        var page = new PortalPage(
            Guid.NewGuid(),
            request.SiteId,
            template.Id,
            template.Version,
            request.Title.Trim(),
            slug,
            request.Summary.Trim(),
            PortalPageStatus.Draft,
            template.Blocks.Select(block => block with { Id = Guid.NewGuid() }).ToArray(),
            request.ActorId,
            request.ActorId,
            now,
            now,
            null);

        await _pageStore.UpsertAsync(page, cancellationToken);
        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            request.ActorId,
            "portal-page.created",
            "PortalPage",
            page.Id.ToString("D"),
            $"Created page '{page.Title}' from template '{template.Code}' version {template.Version}.",
            null), cancellationToken);

        return new PortalPageOperationResult(true, page, Array.Empty<string>());
    }

    public async Task<PortalPageOperationResult> PublishAsync(
        Guid siteId,
        string slug,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var page = await _pageStore.FindBySlugAsync(siteId, SlugGenerator.Generate(slug), cancellationToken);
        if (page is null || page.Status == PortalPageStatus.Archived)
        {
            return new PortalPageOperationResult(false, null, new[] { "Page is not available for publishing." });
        }

        if (page.Status == PortalPageStatus.Published)
        {
            return new PortalPageOperationResult(true, page, Array.Empty<string>());
        }

        var now = _clock();
        var published = page with
        {
            Status = PortalPageStatus.Published,
            PublishedAt = now,
            UpdatedAt = now,
            UpdatedBy = actorId,
            Revision = page.Revision + 1
        };
        await _pageStore.UpsertAsync(published, cancellationToken);
        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            actorId,
            "portal-page.published",
            "PortalPage",
            published.Id.ToString("D"),
            $"Published page '{published.Title}'.",
            null), cancellationToken);
        await EnqueuePublicPageChangeAsync(published, now, cancellationToken);

        return new PortalPageOperationResult(true, published, Array.Empty<string>());
    }

    private async Task EnqueuePublicPageChangeAsync(
        PortalPage page,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var integrationEvent = new PortalPagePublishedIntegrationEvent(
            Guid.NewGuid(),
            occurredAt,
            $"portal-page:{page.Id:D}:revision:{page.Revision}",
            page.SiteId,
            page.Id,
            page.Slug,
            page.Title,
            page.Summary,
            page.PublishedAt ?? occurredAt);
        await _outboxMessageStore.AddAsync(
            OutboxMessageFactory.FromIntegrationEvent(integrationEvent),
            cancellationToken);
    }

    private static List<string> ValidateCreateRequest(CreatePageFromTemplateRequest request)
    {
        var errors = new List<string>();
        if (request.SiteId == Guid.Empty)
        {
            errors.Add("Site identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            errors.Add("Template code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add("Page title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            errors.Add("Page slug is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            errors.Add("Page summary is required.");
        }

        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdatePortalPageRequest request)
    {
        var errors = new List<string>();
        if (request.SiteId == Guid.Empty) errors.Add("Site identifier is required.");
        if (string.IsNullOrWhiteSpace(request.CurrentSlug)) errors.Add("Current page slug is required.");
        if (string.IsNullOrWhiteSpace(request.Title)) errors.Add("Page title is required.");
        if (string.IsNullOrWhiteSpace(request.Slug)) errors.Add("Page slug is required.");
        if (string.IsNullOrWhiteSpace(request.Summary)) errors.Add("Page summary is required.");
        if (request.Blocks.Count == 0) errors.Add("A page must contain at least one block.");
        if (request.ExpectedRevision <= 0) errors.Add("Expected page revision must be positive.");
        return errors;
    }

    private static PortalPageOperationResult RevisionConflict() => new(
        false,
        null,
        new[] { RevisionConflictMessage });

    public IReadOnlyList<string> ValidateDraft(PortalPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(page.Title)) errors.Add("Page title is required.");
        if (string.IsNullOrWhiteSpace(page.Slug)) errors.Add("Page slug is required.");
        if (string.IsNullOrWhiteSpace(page.Summary)) errors.Add("Page summary is required.");
        var blockValidation = PageTemplateValidator.Validate(new PageTemplate(
            page.TemplateId,
            "page-validation",
            page.Title,
            page.Summary,
            PageTemplateStatus.Draft,
            Math.Max(1, page.TemplateVersion),
            page.Blocks,
            page.CreatedBy,
            page.UpdatedBy,
            page.CreatedAt,
            page.UpdatedAt), _blockCatalog);
        errors.AddRange(blockValidation.Errors);
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }
}
