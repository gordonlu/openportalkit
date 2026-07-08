namespace OpenPortalKit.Modules.Data.Datasets;

public sealed record PublicDataSetDetail(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<PublicDataRecord> Records);
