namespace OpenPortalKit.Modules.IndustryPacks;

public sealed class IndustryPackCatalog
{
    private readonly IndustryPackLoader _loader;

    public IndustryPackCatalog(IndustryPackLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public async Task<IndustryPackCatalogResult> DiscoverAsync(
        string packsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packsRoot);
        if (!Directory.Exists(packsRoot))
        {
            return new IndustryPackCatalogResult(
                Array.Empty<LoadedIndustryPack>(),
                new[] { new IndustryPackValidationError("catalog_root_missing", "Industry pack root does not exist.", packsRoot) });
        }

        var packs = new List<LoadedIndustryPack>();
        var errors = new List<IndustryPackValidationError>();
        foreach (var directory in Directory.EnumerateDirectories(packsRoot).Order(StringComparer.OrdinalIgnoreCase))
        {
            var result = await _loader.LoadAsync(directory, cancellationToken);
            if (result.Succeeded)
            {
                packs.Add(result.Pack!);
            }
            else
            {
                errors.AddRange(result.Errors.Select(error => error with
                {
                    ResourcePath = error.ResourcePath is null
                        ? Path.GetFileName(directory)
                        : Path.Combine(Path.GetFileName(directory), error.ResourcePath).Replace('\\', '/')
                }));
            }
        }

        foreach (var duplicate in packs.GroupBy(pack => pack.Manifest.Name, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add(new IndustryPackValidationError(
                "catalog_name_duplicate",
                $"Pack name '{duplicate.Key}' is used by multiple directories."));
        }

        return new IndustryPackCatalogResult(
            errors.Count == 0 ? packs : Array.Empty<LoadedIndustryPack>(),
            errors);
    }
}
