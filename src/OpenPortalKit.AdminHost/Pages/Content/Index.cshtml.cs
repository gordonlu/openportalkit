using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenPortalKit.Modules.Content.ContentItems;

namespace OpenPortalKit.AdminHost.Pages.Content;

public sealed class IndexModel(IContentItemStore contentStore) : PageModel
{
    public const int PageSize = 20;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public Guid? Selected { get; set; }

    public IReadOnlyList<ContentItem> Items { get; private set; } = Array.Empty<ContentItem>();
    public ContentItem? SelectedItem { get; private set; }
    public IReadOnlyList<ContentItemRevision> Versions { get; private set; } = Array.Empty<ContentItemRevision>();
    public int TotalCount { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public ContentPublicationStatus? StatusFilter { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        if (Search?.Length > 200)
        {
            ModelState.AddModelError(nameof(Search), "Search must be at most 200 characters.");
            Search = Search[..200];
        }
        if (!string.IsNullOrWhiteSpace(Status))
        {
            if (Enum.TryParse<ContentPublicationStatus>(Status, ignoreCase: true, out var parsedStatus))
            {
                StatusFilter = parsedStatus;
                Status = parsedStatus.ToString();
            }
            else
            {
                ModelState.AddModelError(nameof(Status), "Select a valid publication status.");
                Status = null;
            }
        }

        PageNumber = Math.Max(1, PageNumber);
        var result = await QueryAsync(PageNumber, cancellationToken);
        TotalCount = result.TotalCount;
        if (TotalCount > 0 && PageNumber > TotalPages)
        {
            PageNumber = TotalPages;
            result = await QueryAsync(PageNumber, cancellationToken);
        }
        Items = result.Items;
        SelectedItem = Items.FirstOrDefault(item => item.Id == Selected) ?? Items.FirstOrDefault();
        Selected = SelectedItem?.Id;
        if (SelectedItem is not null)
            Versions = await contentStore.ListVersionsAsync(SelectedItem.Id, cancellationToken);
    }

    public string BuildPageUrl(int pageNumber, Guid? selected = null)
    {
        return Url.Page("/Content/Index", new
        {
            q = Search,
            status = Status,
            p = pageNumber,
            selected
        }) ?? "/Content";
    }

    public static string AuthorLabel(ContentItem item) => item.AuthorId is { } authorId
        ? authorId.ToString("N")[..8]
        : "Unassigned";

    private Task<AdminContentListPage> QueryAsync(int pageNumber, CancellationToken cancellationToken) =>
        contentStore.ListAdminAsync(new AdminContentListQuery(
            Search: Search,
            Status: StatusFilter,
            Skip: (pageNumber - 1) * PageSize,
            Take: PageSize), cancellationToken);
}
