namespace OpenPortalKit.Modules.IndustryPacks;

public sealed class IndustryPackManifest
{
    public string ManifestVersion { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string RequiresCore { get; init; } = string.Empty;
    public IndustryPackRegistration Registers { get; init; } = new();
    public IndustryPackResourceManifest Resources { get; init; } = new();
}

public sealed class IndustryPackRegistration
{
    public bool ContentTypes { get; init; }
    public bool Datasets { get; init; }
    public bool Rules { get; init; }
    public bool Templates { get; init; }
    public bool DashboardCards { get; init; }
    public bool SeedData { get; init; }
}

public sealed class IndustryPackResourceManifest
{
    public IReadOnlyList<string> ContentTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Datasets { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Rules { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Templates { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DashboardCards { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SeedData { get; init; } = Array.Empty<string>();

    public IEnumerable<(IndustryPackResourceKind Kind, string Path)> Enumerate()
    {
        return ContentTypes.Select(path => (IndustryPackResourceKind.ContentType, path))
            .Concat(Datasets.Select(path => (IndustryPackResourceKind.DataSet, path)))
            .Concat(Rules.Select(path => (IndustryPackResourceKind.Rule, path)))
            .Concat(Templates.Select(path => (IndustryPackResourceKind.Template, path)))
            .Concat(DashboardCards.Select(path => (IndustryPackResourceKind.DashboardCard, path)))
            .Concat(SeedData.Select(path => (IndustryPackResourceKind.SeedData, path)));
    }
}
