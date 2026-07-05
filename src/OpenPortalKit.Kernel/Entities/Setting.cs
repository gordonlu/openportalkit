namespace OpenPortalKit.Kernel.Entities;

public sealed record Setting(
    Guid Id,
    string Scope,
    string Key,
    string Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
