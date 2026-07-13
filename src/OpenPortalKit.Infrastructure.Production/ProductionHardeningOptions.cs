namespace OpenPortalKit.Infrastructure.Production;

public sealed class ProductionHardeningOptions
{
    public const string SectionName = "OpenPortalKit:Production";

    public bool EnableHttpsRedirection { get; set; } = true;
    public bool EnableHsts { get; set; } = true;
    public bool EnableRateLimiting { get; set; } = true;
    public int PublicRequestsPerMinute { get; set; } = 300;
    public int AdminRequestsPerMinute { get; set; } = 120;
    public int QueueLimit { get; set; } = 20;
    public int LoginAttemptsPerFiveMinutes { get; set; } = 10;
    public int HstsMaxAgeDays { get; set; } = 180;
}
