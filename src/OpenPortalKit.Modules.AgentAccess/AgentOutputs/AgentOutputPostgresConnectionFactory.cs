using System.Data.Common;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed class AgentOutputPostgresConnectionFactory : IAgentOutputDbConnectionFactory
{
    private readonly AgentOutputPostgresStorageOptions _options;

    public AgentOutputPostgresConnectionFactory(AgentOutputPostgresStorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Agent output PostgreSQL storage requires a connection string.");
        }

        var factory = DbProviderFactories.GetFactory(_options.ProviderInvariantName);
        var connection = factory.CreateConnection() ??
            throw new InvalidOperationException($"Provider '{_options.ProviderInvariantName}' did not create a connection.");

        connection.ConnectionString = _options.ConnectionString;
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
