namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class AgentBotPolicyOptions
{
    public const string SectionName = "OpenPortalKit:AgentAccess:BotPolicy";

    public bool AllowSearchBots { get; set; } = true;

    public bool AllowTrainingBots { get; set; }

    public string[] AllowedUserAgents { get; set; } = Array.Empty<string>();

    public int? CrawlDelaySeconds { get; set; }

    public AgentBotPolicy ToPolicy()
    {
        return new AgentBotPolicy(
            AllowSearchBots,
            AllowTrainingBots,
            AllowedUserAgents
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CrawlDelaySeconds is > 0 ? CrawlDelaySeconds : null);
    }
}
