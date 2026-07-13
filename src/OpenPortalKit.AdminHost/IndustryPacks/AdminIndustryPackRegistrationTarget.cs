using System.Text.Json;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Data.Datasets;
using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.AdminHost.IndustryPacks;

internal sealed class AdminIndustryPackRegistrationTarget : IIndustryPackResourceRegistrationTarget
{
    private static readonly Guid SystemActorId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid DefaultSiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly IndustryPackRuntimeRegistry _registry;
    private readonly IBlockDefinitionCatalog _blockCatalog;
    private readonly PageTemplateService _templateService;
    private readonly IDataSetStore _dataSetStore;

    public AdminIndustryPackRegistrationTarget(
        IndustryPackResourceKind kind,
        IndustryPackRuntimeRegistry registry,
        IBlockDefinitionCatalog blockCatalog,
        PageTemplateService templateService,
        IDataSetStore dataSetStore)
    {
        Kind = kind;
        _registry = registry;
        _blockCatalog = blockCatalog;
        _templateService = templateService;
        _dataSetStore = dataSetStore;
    }

    public IndustryPackResourceKind Kind { get; }

    public bool RequiresStartupRehydration => Kind is not IndustryPackResourceKind.Template;

    public Task<IReadOnlyList<string>> ValidateAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken = default)
    {
        var propertyName = GetCollectionProperty(Kind);
        var errors = new List<string>();
        if (!resource.Document.TryGetProperty(propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0)
        {
            errors.Add($"Resource '{resource.RelativePath}' requires a non-empty '{propertyName}' array.");
            return Task.FromResult<IReadOnlyList<string>>(errors);
        }

        if (Kind == IndustryPackResourceKind.Template)
        {
            foreach (var template in values.EnumerateArray())
            {
                if (!template.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"Template in '{resource.RelativePath}' requires a blocks array.");
                    continue;
                }

                foreach (var blockCode in blocks.EnumerateArray().Select(value => value.GetString()))
                {
                    if (string.IsNullOrWhiteSpace(blockCode) || _blockCatalog.FindByCode(blockCode) is null)
                    {
                        errors.Add($"Template in '{resource.RelativePath}' references unknown block '{blockCode}'.");
                    }
                }
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public async Task ApplyAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken = default)
    {
        _registry.Upsert(pack.Manifest.Name, resource);
        if (Kind == IndustryPackResourceKind.Template)
        {
            await ApplyTemplatesAsync(pack, resource, cancellationToken);
        }
        else if (Kind == IndustryPackResourceKind.DataSet)
        {
            await ApplyDataSetsAsync(pack, resource, cancellationToken);
        }
    }

    public Task DisableAsync(LoadedIndustryPack pack, CancellationToken cancellationToken = default)
    {
        _registry.Remove(pack.Manifest.Name, Kind);
        return Task.CompletedTask;
    }

    private async Task ApplyTemplatesAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken)
    {
        foreach (var item in resource.Document.GetProperty("templates").EnumerateArray())
        {
            var sourceCode = item.GetProperty("code").GetString()!;
            var packPrefix = pack.Manifest.Name.ToLowerInvariant() + "-";
            var code = sourceCode.StartsWith(packPrefix, StringComparison.OrdinalIgnoreCase)
                ? sourceCode
                : packPrefix + sourceCode;
            var existing = await _templateService.FindByCodeAsync(code, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var template = new PageTemplate(
                existing?.Id ?? Guid.NewGuid(),
                code,
                item.GetProperty("name").GetString()!,
                $"Installed from {pack.Manifest.DisplayName} {pack.Manifest.Version}.",
                PageTemplateStatus.Published,
                existing?.Version ?? 1,
                PageTemplateSeedCatalog.CreateDefaultBlocks(item.GetProperty("blocks").EnumerateArray().Select(value => value.GetString()!)),
                existing?.CreatedBy ?? SystemActorId,
                SystemActorId,
                existing?.CreatedAt ?? now,
                now);
            var result = await _templateService.SaveAsync(template, SystemActorId, cancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors));
            }
        }
    }

    private async Task ApplyDataSetsAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken)
    {
        foreach (var item in resource.Document.GetProperty("datasets").EnumerateArray())
        {
            var sourceCode = item.GetProperty("code").GetString()!;
            var code = $"{pack.Manifest.Name.ToLowerInvariant()}_{sourceCode}";
            var existing = await _dataSetStore.FindDataSetByCodeAsync(DefaultSiteId, code, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var dataSet = new DataSet(
                existing?.Id ?? Guid.NewGuid(), DefaultSiteId, code, item.GetProperty("name").GetString()!,
                $"Installed from {pack.Manifest.DisplayName} {pack.Manifest.Version}.", true,
                existing?.CreatedAt ?? now, now);
            await _dataSetStore.AddDataSetAsync(dataSet, cancellationToken);
            var schemaJson = item.GetProperty("recordSchema").GetRawText();
            var checksum = DataChecksum.FromJson(schemaJson);
            var latest = await _dataSetStore.FindLatestSchemaVersionAsync(dataSet.Id, cancellationToken);
            if (latest is null || !string.Equals(latest.Checksum, checksum, StringComparison.Ordinal))
            {
                await _dataSetStore.AddSchemaVersionAsync(new DataSchemaVersion(
                    Guid.NewGuid(), dataSet.Id, (latest?.VersionNumber ?? 0) + 1, schemaJson, checksum, now), cancellationToken);
            }
        }
    }

    private static string GetCollectionProperty(IndustryPackResourceKind kind) => kind switch
    {
        IndustryPackResourceKind.ContentType => "contentTypes",
        IndustryPackResourceKind.DataSet => "datasets",
        IndustryPackResourceKind.Template => "templates",
        IndustryPackResourceKind.Rule => "rules",
        IndustryPackResourceKind.DashboardCard => "cards",
        IndustryPackResourceKind.SeedData => "records",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
