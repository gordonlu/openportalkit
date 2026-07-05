namespace OpenPortalKit.Kernel.Entities;

public sealed record Permission(
    Guid Id,
    string Code,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
