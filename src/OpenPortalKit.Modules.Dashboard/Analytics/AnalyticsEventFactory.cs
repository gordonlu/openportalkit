using System.Net;
using System.Text;

namespace OpenPortalKit.Modules.Dashboard.Analytics;

public sealed class AnalyticsEventFactory
{
    private readonly AnalyticsPrivacyOptions _options;

    public AnalyticsEventFactory(AnalyticsPrivacyOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public AnalyticsEvent Create(
        string siteId,
        string eventType,
        string path,
        string rawSessionId,
        DateTimeOffset occurredAt,
        string? referrer = null,
        string? userAgent = null,
        string? ipAddress = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return AnalyticsEvent.Create(
            siteId,
            eventType,
            path,
            HashSessionId(rawSessionId),
            occurredAt,
            referrer,
            userAgent,
            _options.AnonymizeIpAddresses ? AnonymizeIpAddress(ipAddress) : ipAddress,
            IsBotTraffic(userAgent),
            metadata);
    }

    public string HashSessionId(string rawSessionId)
    {
        if (string.IsNullOrWhiteSpace(rawSessionId))
        {
            throw new ArgumentException("Session id is required.", nameof(rawSessionId));
        }

        var input = Encoding.UTF8.GetBytes(_options.SessionHashSalt + ":" + rawSessionId);
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offset;
        foreach (var value in input)
        {
            hash ^= value;
            hash *= prime;
        }

        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool IsBotTraffic(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return false;
        }

        return _options.BotUserAgentKeywords.Any(keyword =>
            userAgent.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public static string? AnonymizeIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var parsed))
        {
            return null;
        }

        var bytes = parsed.GetAddressBytes();
        if (bytes.Length == 4)
        {
            bytes[3] = 0;
            return new IPAddress(bytes).ToString();
        }

        if (bytes.Length == 16)
        {
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes).ToString();
        }

        return null;
    }
}
