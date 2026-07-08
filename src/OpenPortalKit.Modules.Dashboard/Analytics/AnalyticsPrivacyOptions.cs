namespace OpenPortalKit.Modules.Dashboard.Analytics;

public sealed class AnalyticsPrivacyOptions
{
    public const string SectionName = "OpenPortalKit:AnalyticsPrivacy";

    public string SessionHashSalt { get; set; } = "openportalkit-local-dev";

    public bool AnonymizeIpAddresses { get; set; } = true;

    public int RetentionDays { get; set; } = 180;

    public bool AllowCrossSiteTracking { get; set; } = false;

    public bool AllowThirdPartyCookies { get; set; } = false;

    public string[] BotUserAgentKeywords { get; set; } =
    {
        "bot",
        "crawler",
        "spider",
        "slurp",
        "agent"
    };
}
