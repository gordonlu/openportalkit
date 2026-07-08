using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;

namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed class ContentPublishingService
{
    private readonly AuditRecorder _auditRecorder;
    private readonly IOutboxMessageStore _outboxMessageStore;

    public ContentPublishingService(
        AuditRecorder auditRecorder,
        IOutboxMessageStore outboxMessageStore)
    {
        _auditRecorder = auditRecorder ?? throw new ArgumentNullException(nameof(auditRecorder));
        _outboxMessageStore = outboxMessageStore ?? throw new ArgumentNullException(nameof(outboxMessageStore));
    }

    public async Task<ContentPublishResult> PublishAsync(
        ContentItem item,
        PublishContentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(request);

        var publishedAt = request.PublishedAt ?? DateTimeOffset.UtcNow;
        var slug = string.IsNullOrWhiteSpace(item.Slug)
            ? SlugGenerator.Generate(item.Title)
            : SlugGenerator.Generate(item.Slug);

        var candidate = item with
        {
            Slug = slug,
            Status = ContentPublicationStatus.Published,
            PublishedAt = publishedAt,
            UpdatedAt = publishedAt,
            UpdatedBy = request.ActorId
        };

        var validation = ContentPublishValidator.ValidateForPublish(candidate);
        if (!validation.IsValid)
        {
            return ContentPublishResult.Failed(validation.Errors);
        }

        var version = ContentVersion.FromContentItem(
            candidate,
            request.VersionNumber,
            request.ActorId,
            publishedAt);

        var auditLog = await _auditRecorder.RecordAsync(
            new AuditRecordRequest(
                request.ActorId,
                "ContentPublished",
                "ContentItem",
                candidate.Id.ToString("D"),
                $"Published content '{candidate.Title}'.",
                MetadataJson: null),
            cancellationToken);

        var integrationEvent = new ContentPublishedIntegrationEvent(
            Guid.NewGuid(),
            publishedAt,
            $"content:{candidate.Id:D}:published:{request.VersionNumber}",
            candidate.SiteId,
            candidate.Id,
            candidate.Slug,
            publishedAt);

        var outboxMessage = await _outboxMessageStore.AddAsync(
            OutboxMessageFactory.FromIntegrationEvent(integrationEvent),
            cancellationToken);

        return new ContentPublishResult(
            Succeeded: true,
            candidate,
            version,
            auditLog,
            outboxMessage,
            Array.Empty<string>());
    }
}
