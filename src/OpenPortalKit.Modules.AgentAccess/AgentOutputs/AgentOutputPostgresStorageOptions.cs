namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class AgentOutputPostgresStorageOptions
{
    public const string SectionName = "OpenPortalKit:AgentAccess:PostgreSQL";

    public bool Enabled { get; set; }

    public string ProviderInvariantName { get; set; } = "Npgsql";

    public string ConnectionStringName { get; set; } = "Default";

    public string? ConnectionString { get; set; }
}
