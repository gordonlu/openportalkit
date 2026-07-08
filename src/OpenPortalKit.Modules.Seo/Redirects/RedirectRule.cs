namespace OpenPortalKit.Modules.Seo.Redirects;

public sealed record RedirectRule(
    Guid Id,
    string SourcePath,
    string Target,
    RedirectStatus Status,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
