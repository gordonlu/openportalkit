namespace OpenPortalKit.Modules.Dashboard.Sources;

public sealed record AgentReadinessPageSignal(
    string PageKey,
    string Url,
    decimal ReadinessScore,
    bool HasMarkdownSnapshot,
    bool HasJsonSnapshot,
    bool IncludedInSitemap,
    bool IncludedInLlmsTxt,
    bool HasStructuredData,
    bool PublicOpenApiAvailable,
    int AgentFacingErrorCount = 0);
