namespace OpenPortalKit.Kernel.Audit;

public sealed record AuditLog(
    Guid Id,
    Guid? ActorId,
    string Action,
    string TargetType,
    string TargetId,
    string? Summary,
    string? MetadataJson,
    DateTimeOffset OccurredAt);
