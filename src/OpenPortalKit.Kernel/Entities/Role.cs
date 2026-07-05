namespace OpenPortalKit.Kernel.Entities;

public sealed record Role(
    Guid Id,
    string Code,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
