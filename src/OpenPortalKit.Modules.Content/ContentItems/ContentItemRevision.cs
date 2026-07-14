namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record ContentItemRevision(
    Guid ContentItemId,
    int Revision,
    ContentItem Snapshot,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
