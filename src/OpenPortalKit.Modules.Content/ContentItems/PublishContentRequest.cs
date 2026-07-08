namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record PublishContentRequest(
    Guid ActorId,
    int VersionNumber,
    DateTimeOffset? PublishedAt = null);
