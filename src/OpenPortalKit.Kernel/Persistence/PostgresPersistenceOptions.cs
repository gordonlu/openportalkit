namespace OpenPortalKit.Kernel.Persistence;

public sealed class PostgresPersistenceOptions
{
    public const string SectionName = "OpenPortalKit:Persistence:PostgreSQL";

    public bool Enabled { get; set; }

    public string ProviderInvariantName { get; set; } = "Npgsql";

    public string ConnectionStringName { get; set; } = "Default";

    public string? ConnectionString { get; set; }
}
