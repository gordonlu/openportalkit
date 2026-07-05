namespace OpenPortalKit.Kernel.Entities;

public sealed record Site(
    Guid Id,
    string Code,
    string Name,
    Uri BaseUri,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
