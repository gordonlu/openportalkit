using System.Text.Json;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;
using OpenPortalKit.Modules.Seo.PublicResources;

namespace OpenPortalKit.Modules.Seo.Revalidation;

public sealed class PublishingRevalidationPlanner
{
    public PublicOutputRevalidationPlan CreatePlan(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.EventName switch
        {
            PublishingEventNames.ContentPublished => CreateContentPublishedPlan(message),
            PublishingEventNames.ContentUpdated => CreateContentPublishedPlan(message),
            PublishingEventNames.ContentArchived => CreateContentArchivedPlan(message),
            _ => throw new InvalidOperationException(
                $"Event '{message.EventName}' does not have a public output revalidation plan.")
        };
    }

    private static PublicOutputRevalidationPlan CreateContentPublishedPlan(OutboxMessage message)
    {
        var slug = ReadRequiredString(message.PayloadJson, "Slug");
        var path = "/content/" + CanonicalUrlBuilder.NormalizePath(slug).Trim('/');

        return new PublicOutputRevalidationPlan(
            message.EventName,
            message.IdempotencyKey,
            message.OccurredAt,
            new[] { path, "/content", "/sitemap.xml", "/rss.xml" },
            RegenerateSitemap: true,
            RegenerateRss: true,
            RegenerateSnapshots: true,
            InvalidateRouteCache: true,
            WarmImportantPages: true);
    }

    private static PublicOutputRevalidationPlan CreateContentArchivedPlan(OutboxMessage message)
    {
        var slug = ReadRequiredString(message.PayloadJson, "Slug");
        var path = "/content/" + CanonicalUrlBuilder.NormalizePath(slug).Trim('/');

        return new PublicOutputRevalidationPlan(
            message.EventName,
            message.IdempotencyKey,
            message.OccurredAt,
            new[] { path, "/content", "/sitemap.xml", "/rss.xml" },
            RegenerateSitemap: true,
            RegenerateRss: true,
            RegenerateSnapshots: false,
            InvalidateRouteCache: true,
            WarmImportantPages: false);
    }

    private static string ReadRequiredString(string payloadJson, string propertyName)
    {
        using var document = JsonDocument.Parse(payloadJson);

        if (!document.RootElement.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"Publishing event payload must include string property '{propertyName}'.");
        }

        var value = property.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Publishing event payload property '{propertyName}' cannot be empty.");
        }

        return value;
    }
}
