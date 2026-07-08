namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record DataSet(
    Guid Id,
    Guid SiteId,
    string Code,
    string Name,
    string Description,
    bool IsPublic,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
