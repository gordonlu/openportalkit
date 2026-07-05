namespace OpenPortalKit.Kernel.Jobs;

public sealed record JobRecord(
    Guid Id,
    string JobType,
    string Status,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? LastError);
