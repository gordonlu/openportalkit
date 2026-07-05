namespace OpenPortalKit.Kernel.Entities;

public sealed record Asset(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
