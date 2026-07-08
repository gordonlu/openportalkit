namespace OpenPortalKit.Modules.Search.Indexing;

public sealed record SearchQuery(
    string Term,
    bool IncludeNonPublic = false,
    bool IncludeArchived = false,
    IReadOnlyList<string>? TargetTypes = null,
    IReadOnlyList<string>? Tags = null,
    DateTimeOffset? AsOf = null,
    int Limit = 20);
