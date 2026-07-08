using System.Data.Common;

namespace OpenPortalKit.Modules.Dashboard.Storage;

public sealed class DashboardPostgresConnectionFactory : IDashboardDbConnectionFactory
{
    private readonly DashboardPostgresStorageOptions _options;

    public DashboardPostgresConnectionFactory(DashboardPostgresStorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ProviderInvariantName);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ConnectionString);

        var provider = DbProviderFactories.GetFactory(_options.ProviderInvariantName);
        var connection = provider.CreateConnection()
            ?? throw new InvalidOperationException($"Provider '{_options.ProviderInvariantName}' did not create a connection.");
        connection.ConnectionString = _options.ConnectionString;
        await connection.OpenAsync(cancellationToken);

        return connection;
    }
}
