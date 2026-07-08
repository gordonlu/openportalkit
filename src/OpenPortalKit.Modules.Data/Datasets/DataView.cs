namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataView(
    Guid Id,
    Guid DataSetId,
    string Code,
    string Name,
    string FilterJson,
    string SortJson,
    string ColumnsJson,
    string PublicSlug,
    string CachePolicy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
