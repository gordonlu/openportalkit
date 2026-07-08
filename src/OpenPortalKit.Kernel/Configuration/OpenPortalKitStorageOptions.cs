namespace OpenPortalKit.Kernel.Configuration;

public sealed record OpenPortalKitStorageOptions
{
    public const string SectionName = "OpenPortalKit:Storage";

    public string Provider { get; init; } = "PostgreSQL";

    public string PrimaryConnectionStringName { get; init; } = "Default";

    public string CacheConnectionStringName { get; init; } = "Redis";
}
