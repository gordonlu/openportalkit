namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PortalPage(
    Guid Id,
    Guid SiteId,
    Guid TemplateId,
    int TemplateVersion,
    string Title,
    string Slug,
    string Summary,
    PortalPageStatus Status,
    IReadOnlyList<BlockInstance> Blocks,
    Guid CreatedBy,
    Guid UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt);
