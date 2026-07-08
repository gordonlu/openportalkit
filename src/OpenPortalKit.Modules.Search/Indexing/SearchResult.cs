namespace OpenPortalKit.Modules.Search.Indexing;

public sealed record SearchResult(
    SearchDocument Document,
    int Score,
    IReadOnlyList<string> MatchedFields);
