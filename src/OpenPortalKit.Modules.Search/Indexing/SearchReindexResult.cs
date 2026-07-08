namespace OpenPortalKit.Modules.Search.Indexing;

public sealed record SearchReindexResult(
    int IndexedDocuments,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
