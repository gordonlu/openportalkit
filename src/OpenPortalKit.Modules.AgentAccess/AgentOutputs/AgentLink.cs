namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentLink(
    string Title,
    Uri Url,
    string? Description = null);
