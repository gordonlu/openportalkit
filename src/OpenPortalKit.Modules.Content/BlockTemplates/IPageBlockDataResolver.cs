namespace OpenPortalKit.Modules.Content.BlockTemplates;

public interface IPageBlockDataResolver
{
    Task<RenderedPageDataTable?> ResolveDataTableAsync(
        Guid siteId,
        string dataSetCode,
        int take,
        CancellationToken cancellationToken = default);
}

public sealed record RenderedPageDataTable(
    string Name,
    string Description,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string SourceUrl);
