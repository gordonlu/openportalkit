namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentVisibilityPolicy(
    bool IncludeInSitemap,
    bool IncludeInLlmsText,
    bool AllowSearchIndexing,
    bool AllowAiTraining,
    bool AllowRagExtraction,
    string AttributionRequirement)
{
    public static AgentVisibilityPolicy Default { get; } = new(
        IncludeInSitemap: true,
        IncludeInLlmsText: true,
        AllowSearchIndexing: true,
        AllowAiTraining: false,
        AllowRagExtraction: true,
        AttributionRequirement: "Cite the canonical URL and preserve source attribution.");
}
