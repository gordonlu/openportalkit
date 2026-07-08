namespace OpenPortalKit.Modules.Dashboard.Storage;

public sealed class DashboardPostgresStorageOptions
{
    public const string SectionName = "OpenPortalKit:Dashboard:PostgreSQL";

    public bool Enabled { get; set; } = false;

    public string ProviderInvariantName { get; set; } = "Npgsql";

    public string ConnectionStringName { get; set; } = "Default";

    public string? ConnectionString { get; set; }
}
