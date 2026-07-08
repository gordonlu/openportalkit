namespace OpenPortalKit.Kernel.Audit;

public sealed record AuditRecordRequest(
    Guid? ActorId,
    string Action,
    string TargetType,
    string TargetId,
    string? Summary = null,
    string? MetadataJson = null);
