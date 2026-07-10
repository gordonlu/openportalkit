namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PageTemplateVersion(
    Guid TemplateId,
    int Version,
    PageTemplate Snapshot,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
