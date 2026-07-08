namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record ContentListQuery(
    Guid? SiteId = null,
    Guid? ContentTypeId = null,
    Guid? CategoryId = null,
    string? Tag = null,
    int Skip = 0,
    int Take = 20);
