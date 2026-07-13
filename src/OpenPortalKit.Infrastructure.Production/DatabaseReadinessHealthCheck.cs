using System.Data.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OpenPortalKit.Infrastructure.Production;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly string _providerInvariantName;
    private readonly string _connectionString;

    public DatabaseReadinessHealthCheck(string providerInvariantName, string connectionString)
    {
        _providerInvariantName = providerInvariantName;
        _connectionString = connectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = DbProviderFactories.GetFactory(_providerInvariantName);
            await using var connection = factory.CreateConnection()
                ?? throw new InvalidOperationException($"Provider '{_providerInvariantName}' did not create a connection.");
            connection.ConnectionString = _connectionString;
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "select 1";
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database accepted a validation query.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Database validation query failed.", exception);
        }
    }
}
