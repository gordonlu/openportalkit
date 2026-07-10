namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PageTemplate(
    Guid Id,
    string Code,
    string Name,
    string Description,
    PageTemplateStatus Status,
    int Version,
    IReadOnlyList<BlockInstance> Blocks,
    Guid CreatedBy,
    Guid UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
