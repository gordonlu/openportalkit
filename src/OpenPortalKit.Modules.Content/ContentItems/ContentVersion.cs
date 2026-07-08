using OpenPortalKit.Kernel.Entities;

namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record ContentVersion(
    Guid Id,
    Guid ContentItemId,
    int VersionNumber,
    string Title,
    string Slug,
    string Summary,
    string Body,
    ContentPublicationStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt) : IEntity
{
    public static ContentVersion FromContentItem(
        ContentItem item,
        int versionNumber,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);

        return new ContentVersion(
            Guid.NewGuid(),
            item.Id,
            versionNumber,
            item.Title,
            item.Slug,
            item.Summary,
            item.Body,
            item.Status,
            createdBy,
            createdAt);
    }
}
