namespace OpenPortalKit.Modules.Content.ContentItems;

public static class ContentPublishValidator
{
    public static ContentValidationResult ValidateForPublish(ContentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            errors.Add("Published content must have a title.");
        }

        if (string.IsNullOrWhiteSpace(item.Slug))
        {
            errors.Add("Published content must have a slug.");
        }

        if (string.IsNullOrWhiteSpace(item.Summary))
        {
            errors.Add("Published content must have a summary.");
        }

        if (item.ScheduledAt is not null && item.Status == ContentPublicationStatus.Published && item.PublishedAt is null)
        {
            errors.Add("Scheduled content must have a published timestamp when it becomes public.");
        }

        return new ContentValidationResult(errors.Count == 0, errors);
    }
}
