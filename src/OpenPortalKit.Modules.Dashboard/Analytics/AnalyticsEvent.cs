using System.Collections.ObjectModel;

namespace OpenPortalKit.Modules.Dashboard.Analytics;

public sealed record AnalyticsEvent(
    Guid Id,
    string SiteId,
    string EventType,
    string Path,
    string HashedSessionId,
    DateTimeOffset OccurredAt,
    string? Referrer,
    string? UserAgent,
    string? AnonymizedIpAddress,
    bool IsBot,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static AnalyticsEvent Create(
        string siteId,
        string eventType,
        string path,
        string hashedSessionId,
        DateTimeOffset occurredAt,
        string? referrer = null,
        string? userAgent = null,
        string? anonymizedIpAddress = null,
        bool isBot = false,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw new ArgumentException("Site id is required.", nameof(siteId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type is required.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(hashedSessionId))
        {
            throw new ArgumentException("Hashed session id is required.", nameof(hashedSessionId));
        }

        return new AnalyticsEvent(
            Guid.NewGuid(),
            siteId,
            eventType,
            path,
            hashedSessionId,
            occurredAt,
            referrer,
            userAgent,
            anonymizedIpAddress,
            isBot,
            new ReadOnlyDictionary<string, string>(metadata?.ToDictionary() ?? new Dictionary<string, string>()));
    }
}
