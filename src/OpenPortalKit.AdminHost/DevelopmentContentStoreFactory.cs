using OpenPortalKit.Modules.Content.ContentItems;

namespace OpenPortalKit.AdminHost;

internal static class DevelopmentContentStoreFactory
{
    private static readonly Guid SiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ContentTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static IContentItemStore Create()
    {
        var store = new InMemoryContentItemStore();
        foreach (var item in CreateItems()) store.AddAsync(item).GetAwaiter().GetResult();
        return store;
    }

    private static IReadOnlyList<ContentItem> CreateItems()
    {
        var now = DateTimeOffset.UtcNow;
        return new[]
        {
            Create("Product documentation refresh", "product-documentation-refresh", ContentPublicationStatus.Review, now.AddMinutes(-18), ["documentation", "public-output"]),
            Create("Research portal release notes", "research-portal-release-notes", ContentPublicationStatus.Approved, now.AddDays(-1), ["release-notes"]),
            Create("Service availability announcement", "service-availability-announcement", ContentPublicationStatus.Published, now.AddDays(-2), ["availability", "announcement"]),
            Create("Structured publishing guide", "structured-publishing-guide", ContentPublicationStatus.Draft, now.AddDays(-4), ["publishing", "guide"]),
            Create("Agent-readable output policy", "agent-readable-output-policy", ContentPublicationStatus.Rejected, now.AddDays(-5), ["agentseo", "policy"])
        };
    }

    private static ContentItem Create(
        string title,
        string slug,
        ContentPublicationStatus status,
        DateTimeOffset updatedAt,
        IReadOnlyList<string> tags) => new(
            Guid.NewGuid(),
            SiteId,
            ContentTypeId,
            title,
            slug,
            $"Development fixture for {title.ToLowerInvariant()}.",
            "Development content body.",
            CoverAssetId: null,
            status,
            CategoryId: null,
            tags,
            ActorId,
            "development-fixture",
            status == ContentPublicationStatus.Published ? updatedAt : null,
            ScheduledAt: null,
            ExpiresAt: null,
            ActorId,
            ActorId,
            updatedAt.AddDays(-2),
            updatedAt);
}
