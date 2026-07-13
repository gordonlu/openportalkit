using Microsoft.Extensions.Configuration;

namespace OpenPortalKit.Infrastructure.Production;

public static class ProductionConfigurationValidator
{
    public static void ValidateWebHost(IConfiguration configuration, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (isDevelopment) return;

        var allowedHosts = configuration["AllowedHosts"]?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (allowedHosts.Length == 0 || allowedHosts.Any(host => host == "*"))
        {
            throw new InvalidOperationException(
                "Production web hosts require an explicit non-wildcard AllowedHosts value.");
        }
    }

    public static void ValidateHttpsEndpoint(
        IConfiguration configuration,
        string key,
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (isDevelopment) return;

        var value = configuration[key];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Production configuration '{key}' must be an absolute HTTPS URI.");
        }
    }
}
