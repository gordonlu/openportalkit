namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record UpdatePortalPageRequest(
    Guid SiteId,
    string CurrentSlug,
    string Title,
    string Slug,
    string Summary,
    IReadOnlyList<BlockInstance> Blocks,
    Guid ActorId);
