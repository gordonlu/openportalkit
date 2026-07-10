using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Modules.Content.ContentItems;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PortalPageService
{
    private readonly IPageTemplateStore _templateStore;
    private readonly IPageStore _pageStore;
    private readonly AuditRecorder _auditRecorder;
    private readonly IOutboxMessageStore _outboxMessageStore;
    private readonly Func<DateTimeOffset> _clock;

    public PortalPageService(
        IPageTemplateStore templateStore,
        IPageStore pageStore,
        AuditRecorder auditRecorder,
        IOutboxMessageStore outboxMessageStore,
        Func<DateTimeOffset>? clock = null)
    {
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _pageStore = pageStore ?? throw new ArgumentNullException(nameof(pageStore));
        _auditRecorder = auditRecorder ?? throw new ArgumentNullException(nameof(auditRecorder));
        _outboxMessageStore = outboxMessageStore ?? throw new ArgumentNullException(nameof(outboxMessageStore));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
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
            UpdatedBy = actorId
        };
        await _pageStore.UpsertAsync(published, cancellationToken);
        await _auditRecorder.RecordAsync(new AuditRecordRequest(
            actorId,
            "portal-page.published",
            "PortalPage",
            published.Id.ToString("D"),
            $"Published page '{published.Title}'.",
            null), cancellationToken);
        var integrationEvent = new PortalPagePublishedIntegrationEvent(
            Guid.NewGuid(),
            now,
            $"portal-page:{published.Id:D}:published:{published.TemplateVersion}:{now.UtcDateTime.Ticks}",
            published.SiteId,
            published.Id,
            published.Slug,
            published.Title,
            published.Summary,
            now);
        await _outboxMessageStore.AddAsync(
            OutboxMessageFactory.FromIntegrationEvent(integrationEvent),
            cancellationToken);

        return new PortalPageOperationResult(true, published, Array.Empty<string>());
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
}
