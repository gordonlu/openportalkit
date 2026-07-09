namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentBotPolicy(
    bool AllowSearchBots,
    bool AllowTrainingBots,
    IReadOnlyList<string> AllowedUserAgents,
    int? CrawlDelaySeconds)
{
    public static AgentBotPolicy Default { get; } = new(
        AllowSearchBots: true,
        AllowTrainingBots: false,
        AllowedUserAgents: Array.Empty<string>(),
        CrawlDelaySeconds: null);
}
