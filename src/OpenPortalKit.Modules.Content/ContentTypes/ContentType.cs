using OpenPortalKit.Kernel.Entities;

namespace OpenPortalKit.Modules.Content.ContentTypes;

public sealed record ContentType(
    Guid Id,
    Guid SiteId,
    string Code,
    string Name,
    string? Description,
    string SchemaJson,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
