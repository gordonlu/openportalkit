namespace OpenPortalKit.Modules.Seo.PublicResources;

public sealed record PublicResourceDescriptor(
    string Title,
    string Description,
    string Path,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    string? Language = null,
    IReadOnlyDictionary<string, string>? Attributes = null);
