namespace OpenPortalKit.Modules.Seo.Redirects;

public sealed record RedirectResolution(
    string SourcePath,
    string Target,
    int StatusCode,
    bool IsExternal);
