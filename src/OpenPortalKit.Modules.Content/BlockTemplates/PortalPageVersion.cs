namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PortalPageVersion(
    Guid PageId,
    int Revision,
    PortalPage Snapshot,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
