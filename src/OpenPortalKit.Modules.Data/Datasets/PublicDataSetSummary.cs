namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record PublicDataSetSummary(
    string Code,
    string Name,
    string Description,
    DateTimeOffset UpdatedAt);
