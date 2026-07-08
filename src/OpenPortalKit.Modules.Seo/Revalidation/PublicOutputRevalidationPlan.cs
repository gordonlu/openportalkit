namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed record PublicOutputRevalidationPlan(
    string SourceEventName,
    string SourceIdempotencyKey,
    DateTimeOffset RequestedAt,
    IReadOnlyList<string> Routes,
    bool RegenerateSitemap,
    bool RegenerateRss,
    bool RegenerateSnapshots,
    bool InvalidateRouteCache,
    bool WarmImportantPages);
