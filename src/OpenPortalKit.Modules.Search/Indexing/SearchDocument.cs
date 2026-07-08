namespace OpenPortalKit.Modules.Search.Indexing;

public sealed record SearchDocument(
    string Id,
    string TargetType,
    string TargetId,
    string Title,
    string Summary,
    string BodyText,
    string Url,
    string ContentType,
    IReadOnlyList<string> Tags,
    string? Category,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt,
    SearchVisibility Visibility,
    string? Language,
    string? MetadataJson);
