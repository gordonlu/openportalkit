namespace OpenPortalKit.Modules.Seo.PublicResources;

public sealed record RobotsDirective(
    string UserAgent,
    IReadOnlyList<string> Allow,
    IReadOnlyList<string> Disallow);
