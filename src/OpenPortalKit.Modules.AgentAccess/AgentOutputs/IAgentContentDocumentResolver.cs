using OpenPortalKit.Modules.Seo.Revalidation;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public interface IAgentContentDocumentResolver
{
    Task<AgentContentDocument?> FindPublishedBySlugAsync(
        PublicOutputRevalidationPlan plan,
        string slug,
        CancellationToken cancellationToken = default);
}
