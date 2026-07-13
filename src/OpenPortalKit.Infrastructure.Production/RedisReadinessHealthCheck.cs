using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OpenPortalKit.Infrastructure.Production;

public sealed class RedisReadinessHealthCheck : IHealthCheck
{
    private static readonly byte[] PingCommand = Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n");
    private readonly string _host;
    private readonly int _port;

    public RedisReadinessHealthCheck(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(PingCommand, timeout.Token);
            var response = new byte[7];
            var read = await stream.ReadAsync(response, timeout.Token);
            return read >= 5 && Encoding.ASCII.GetString(response, 0, read).StartsWith("+PONG", StringComparison.Ordinal)
                ? HealthCheckResult.Healthy("Redis returned PONG.")
                : HealthCheckResult.Unhealthy("Redis returned an unexpected PING response.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Redis PING failed.", exception);
        }
    }

    public static bool TryParseEndpoint(string connectionString, out string host, out int port)
    {
        host = string.Empty;
        port = 6379;
        var endpoint = connectionString.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        if (Uri.TryCreate($"redis://{endpoint}", UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            host = uri.Host;
            port = uri.IsDefaultPort ? 6379 : uri.Port;
            return true;
        }
        return false;
    }
}
