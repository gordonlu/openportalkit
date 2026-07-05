namespace OpenPortalKit.Kernel.Entities;

public sealed record User(
    Guid Id,
    string UserName,
    string DisplayName,
    string Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
