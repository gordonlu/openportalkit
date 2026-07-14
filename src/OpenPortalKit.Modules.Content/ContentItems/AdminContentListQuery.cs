namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record AdminContentListQuery(
    Guid? SiteId = null,
    string? Search = null,
    ContentPublicationStatus? Status = null,
    Guid? ContentTypeId = null,
    Guid? AuthorId = null,
    int Skip = 0,
    int Take = 20);

public sealed record AdminContentListPage(
    IReadOnlyList<ContentItem> Items,
    int TotalCount);
