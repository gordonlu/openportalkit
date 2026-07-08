namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed record PublicOutputRevalidationResult(
    Guid Id,
    string SourceEventName,
    string SourceIdempotencyKey,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyList<string> InvalidatedRoutes,
    IReadOnlyList<string> RegeneratedOutputs,
    bool Succeeded,
    string? Error);
