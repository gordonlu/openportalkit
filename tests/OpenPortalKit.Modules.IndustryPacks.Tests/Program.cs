using OpenPortalKit.Modules.IndustryPacks;
using OpenPortalKit.Kernel.Audit;

var tests = new (string Name, Func<Task> Run)[]
{
    ("catalog discovers four independently valid reference packs", CatalogDiscoversReferencePacks),
    ("loader rejects resources outside the pack root", LoaderRejectsPathTraversal),
    ("loader rejects packs requiring a newer core", LoaderRejectsNewerCore),
    ("loader rejects unsupported manifest versions and unknown properties", LoaderRejectsIncompatibleManifest),
    ("manifest JSON schema describes the enforced v1 contract", ManifestSchemaDescribesV1Contract),
    ("generic pack module remains industry neutral", GenericModuleRemainsIndustryNeutral),
    ("pack enablement is audited and idempotent", PackEnablementIsAuditedAndIdempotent),
    ("installation migration preserves checksummed state", InstallationMigrationPreservesChecksummedState),
    ("enabled packs rehydrate and checksum drift fails closed", EnabledPacksRehydrateAndChecksumDriftFailsClosed)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failures == 0 ? 0 : 1;

static async Task CatalogDiscoversReferencePacks()
{
    var result = await new IndustryPackCatalog(new IndustryPackLoader("0.1.0"))
        .DiscoverAsync("industry-packs");

    Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
    Assert.Equal(4, result.Packs.Count);
    Assert.SequenceEqual(new[] { "Education", "Entertainment", "Finance", "Technology" },
        result.Packs.Select(pack => pack.Manifest.Name).Order(StringComparer.Ordinal).ToArray());
    foreach (var pack in result.Packs)
    {
        Assert.Equal(6, pack.Resources.Count);
        Assert.True(pack.Resources.All(resource => resource.Checksum.Length == 64),
            $"Expected SHA-256 checksums for {pack.Manifest.Name} resources.");
        Assert.True(pack.Resources.All(resource =>
                resource.Document.TryGetProperty("schemaVersion", out var version) &&
                !string.IsNullOrWhiteSpace(version.GetString())),
            $"Expected schemaVersion on every {pack.Manifest.Name} resource.");
    }
}

static async Task LoaderRejectsPathTraversal()
{
    var root = CreateTemporaryPack("Traversal", "0.1.0", "../outside.json");
    try
    {
        var result = await new IndustryPackLoader("0.1.0").LoadAsync(root);
        Assert.False(result.Succeeded, "Expected path traversal to be rejected.");
        Assert.Contains("resource_path_invalid", result.Errors.Select(error => error.Code));
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task LoaderRejectsNewerCore()
{
    var root = CreateTemporaryPack("Future", "9.0.0", "content-types/catalog.json");
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "content-types"));
        await File.WriteAllTextAsync(Path.Combine(root, "content-types", "catalog.json"),
            """{"schemaVersion":"test.v1"}""");
        var result = await new IndustryPackLoader("0.1.0").LoadAsync(root);
        Assert.False(result.Succeeded, "Expected a newer core requirement to be rejected.");
        Assert.Contains("manifest_core_version_unsupported", result.Errors.Select(error => error.Code));
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task LoaderRejectsIncompatibleManifest()
{
    var root = CreateTemporaryPack("FutureManifest", "0.1.0", "content-types/catalog.json", "2.0");
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "content-types"));
        await File.WriteAllTextAsync(Path.Combine(root, "content-types", "catalog.json"),
            """{"schemaVersion":"test.v1"}""");
        var unsupported = await new IndustryPackLoader(IndustryPackContract.CurrentCoreVersion).LoadAsync(root);
        Assert.Contains("manifest_schema_version_unsupported", unsupported.Errors.Select(error => error.Code));

        var manifestPath = Path.Combine(root, "pack.json");
        var manifest = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, manifest.Replace(
            "\"manifestVersion\": \"2.0\",",
            "\"manifestVersion\": \"1.0\",\n  \"unexpected\": true,",
            StringComparison.Ordinal));
        var unknown = await new IndustryPackLoader(IndustryPackContract.CurrentCoreVersion).LoadAsync(root);
        Assert.Contains("manifest_property_unknown", unknown.Errors.Select(error => error.Code));
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static Task ManifestSchemaDescribesV1Contract()
{
    using var document = System.Text.Json.JsonDocument.Parse(
        File.ReadAllText(Path.Combine("schemas", "industry-pack-manifest.v1.schema.json")));
    var root = document.RootElement;
    Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
    Assert.Equal(IndustryPackContract.ManifestVersion,
        root.GetProperty("properties").GetProperty("manifestVersion").GetProperty("const").GetString());
    Assert.False(root.GetProperty("additionalProperties").GetBoolean(), "Manifest schema must reject unknown properties.");
    return Task.CompletedTask;
}

static Task GenericModuleRemainsIndustryNeutral()
{
    var sourceRoot = Path.Combine("src", "OpenPortalKit.Modules.IndustryPacks");
    var source = string.Join('\n', Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
        .Select(File.ReadAllText));
    foreach (var forbidden in new[] { "Fund", "Course", "Student", "ReleaseCatalog", "Talent", "Streaming" })
    {
        Assert.False(source.Contains(forbidden, StringComparison.Ordinal),
            $"Generic industry pack module contains vertical term '{forbidden}'.");
    }

    return Task.CompletedTask;
}

static async Task PackEnablementIsAuditedAndIdempotent()
{
    var pack = (await new IndustryPackLoader("0.1.0").LoadAsync("industry-packs/Technology")).Pack!;
    var store = new InMemoryIndustryPackInstallationStore();
    var auditStore = new InMemoryAuditLogStore();
    var targets = Enum.GetValues<IndustryPackResourceKind>().Select(kind => new RecordingTarget(kind)).ToArray();
    var now = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
    var service = new IndustryPackInstallationService(store, targets, new AuditRecorder(auditStore), () => now);
    var actorId = Guid.NewGuid();

    var first = await service.EnableAsync(pack, actorId);
    var second = await service.EnableAsync(pack, actorId);
    var disabled = await service.DisableAsync(pack, actorId);
    var installation = await store.FindAsync(pack.Manifest.Name);
    var resources = await store.ListResourcesAsync(pack.Manifest.Name);
    var audits = await auditStore.FindByTargetAsync("IndustryPack", pack.Manifest.Name);

    Assert.True(first.Succeeded, "Expected first enablement to succeed.");
    Assert.Equal(6, first.Plan.ChangedResourceCount);
    Assert.True(second.Succeeded, "Expected repeated enablement to succeed.");
    Assert.Equal(0, second.Plan.ChangedResourceCount);
    Assert.Equal(6, targets.Sum(target => target.ApplyCount));
    Assert.True(disabled.Succeeded, "Expected disablement to succeed.");
    Assert.False(installation!.IsEnabled, "Expected installation to be disabled.");
    Assert.Equal(6, resources.Count);
    Assert.Equal(3, audits.Count);
    Assert.Equal("industry-pack.disabled", audits[0].Action);
}

static Task InstallationMigrationPreservesChecksummedState()
{
    var sql = File.ReadAllText(Path.Combine("db", "postgresql", "migrations", "0013_industry_pack_installations.sql"));
    Assert.ContainsText("create table if not exists opk_industry_pack_installations", sql);
    Assert.ContainsText("create table if not exists opk_industry_pack_resources", sql);
    Assert.ContainsText("length(manifest_checksum) = 64", sql);
    Assert.ContainsText("primary key (pack_name, resource_path)", sql);
    return Task.CompletedTask;
}

static async Task EnabledPacksRehydrateAndChecksumDriftFailsClosed()
{
    var pack = (await new IndustryPackLoader("0.1.0").LoadAsync("industry-packs/Education")).Pack!;
    var store = new InMemoryIndustryPackInstallationStore();
    var targets = Enum.GetValues<IndustryPackResourceKind>().Select(kind => new RecordingTarget(kind)).ToArray();
    var service = new IndustryPackInstallationService(
        store, targets, new AuditRecorder(new InMemoryAuditLogStore()),
        () => new DateTimeOffset(2026, 7, 13, 11, 0, 0, TimeSpan.Zero));
    await service.EnableAsync(pack, Guid.NewGuid());

    var rehydrated = await service.RehydrateEnabledAsync(new[] { pack });
    var drifted = await service.RehydrateEnabledAsync(new[] { pack with { ManifestChecksum = new string('0', 64) } });

    Assert.True(rehydrated.Succeeded, "Expected enabled pack rehydration to succeed.");
    Assert.Equal(1, rehydrated.RehydratedPackCount);
    Assert.Equal(6, rehydrated.RehydratedResourceCount);
    Assert.False(drifted.Succeeded, "Expected checksum drift to fail closed.");
    Assert.True(drifted.Errors.Any(error => error.Contains("changed on disk", StringComparison.Ordinal)),
        "Expected checksum drift error.");
}

static string CreateTemporaryPack(
    string name,
    string requiresCore,
    string resourcePath,
    string manifestVersion = IndustryPackContract.ManifestVersion)
{
    var root = Path.Combine(Path.GetTempPath(), "opk-pack-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    File.WriteAllText(Path.Combine(root, "pack.json"), $$"""
        {
          "manifestVersion": "{{manifestVersion}}",
          "name": "{{name}}",
          "displayName": "{{name}} Pack",
          "description": "Test pack.",
          "version": "0.1.0",
          "requiresCore": "{{requiresCore}}",
          "registers": {
            "contentTypes": true,
            "datasets": false,
            "rules": false,
            "templates": false,
            "dashboardCards": false,
            "seedData": false
          },
          "resources": {
            "contentTypes": ["{{resourcePath}}"],
            "datasets": [],
            "rules": [],
            "templates": [],
            "dashboardCards": [],
            "seedData": []
          }
        }
        """);
    return root;
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message)
    {
        if (condition) throw new InvalidOperationException(message);
    }

    public static void Contains(string expected, IEnumerable<string> values)
    {
        if (!values.Contains(expected, StringComparer.Ordinal))
            throw new InvalidOperationException($"Expected collection to contain '{expected}'.");
    }

    public static void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Sequences do not match.");
    }

    public static void ContainsText(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected text to contain '{expected}'.");
    }
}

internal sealed class RecordingTarget : IIndustryPackResourceRegistrationTarget
{
    public RecordingTarget(IndustryPackResourceKind kind) => Kind = kind;
    public IndustryPackResourceKind Kind { get; }
    public bool RequiresStartupRehydration => true;
    public int ApplyCount { get; private set; }
    public int DisableCount { get; private set; }

    public Task<IReadOnlyList<string>> ValidateAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task ApplyAsync(
        LoadedIndustryPack pack,
        IndustryPackResource resource,
        CancellationToken cancellationToken = default)
    {
        ApplyCount++;
        return Task.CompletedTask;
    }

    public Task DisableAsync(LoadedIndustryPack pack, CancellationToken cancellationToken = default)
    {
        DisableCount++;
        return Task.CompletedTask;
    }
}
