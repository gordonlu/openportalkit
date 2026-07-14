using System.Net;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OpenPortalKit.Cli;
using OpenPortalKit.Cli.Checks;

var tests = new (string Name, Func<Task> Run)[]
{
    ("CLI exposes stable help and usage errors", CliExposesStableHelpAndUsageErrors),
    ("branding contract validates assets and blocks unsafe configuration", BrandingContractIsStrict),
    ("repository boundary check passes the product tree", RepositoryBoundaryCheckPasses),
    ("boundary check detects industry leakage", BoundaryCheckDetectsIndustryLeakage),
    ("repository AgentSEO contract is complete", RepositoryAgentContractIsComplete),
    ("live AgentSEO check validates representations", LiveAgentCheckValidatesRepresentations),
    ("live AgentSEO check rejects cross-origin discovery", LiveAgentCheckRejectsCrossOriginDiscovery),
    ("CLI validates industry packs through the shared contract", CliValidatesIndustryPack),
    ("CLI scaffolds a valid industry pack without overwriting", CliScaffoldsIndustryPack),
    ("CLI produces a traceable legacy dry-run report", CliProducesLegacyDryRunReport),
    ("CLI blocks unsafe legacy rows", CliBlocksUnsafeLegacyRows),
    ("CLI creates a traceable source workspace", CliCreatesTraceableSourceWorkspace),
    ("workspace scaffolding rejects unsafe sources and destinations", WorkspaceScaffoldingRejectsUnsafePaths),
    ("project profiles reject malformed or unresolved manifests", ProjectProfilesRejectInvalidManifests),
    ("offline template archives verify provenance and reject traversal", OfflineTemplateArchivesAreVerified),
    ("repository workspace scaffold preserves product boundaries", RepositoryWorkspaceScaffoldPreservesBoundaries)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static async Task CliExposesStableHelpAndUsageErrors()
{
    var output = new StringWriter();
    var error = new StringWriter();
    var application = new CliApplication(output, error);

    Assert(await application.RunAsync(["--help"]) == 0, "Help should succeed.");
    Assert(output.ToString().Contains("check-agent-readiness", StringComparison.Ordinal), "Help omitted a command.");
    Assert(output.ToString().Contains("opk new", StringComparison.Ordinal), "Help omitted project scaffolding.");
    Assert(output.ToString().Contains("opk module add", StringComparison.Ordinal), "Help omitted module scaffolding.");
    Assert(output.ToString().Contains("opk upgrade inspect", StringComparison.Ordinal), "Help omitted upgrade inspection.");
    Assert(output.ToString().Contains("opk branding validate", StringComparison.Ordinal), "Help omitted branding validation.");
    Assert(await application.RunAsync(["unknown"]) == 2, "Unknown commands should return usage exit code 2.");
}

static async Task BrandingContractIsStrict()
{
    var repository = FindRepositoryRoot();
    var validator = new OpenPortalKit.Cli.Authoring.BrandingManifestValidator();
    var repositoryResult = await validator.ValidateAsync(repository);
    Assert(repositoryResult.Succeeded,
        string.Join("; ", repositoryResult.Errors.Select(item => item.Code + ": " + item.Message)));

    var root = CreateTestDirectory();
    try
    {
        var lib = Path.Combine(root, "apps", "web", "src", "lib");
        var app = Path.Combine(root, "apps", "web", "src", "app");
        var examples = Path.Combine(root, "apps", "web", "public", "examples");
        Directory.CreateDirectory(lib);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(examples);
        await File.WriteAllTextAsync(Path.Combine(root, "OpenPortalKit.sln"), "branding test workspace");
        var sourceManifest = Path.Combine(repository, "apps", "web", "src", "lib", "branding.json");
        var manifestPath = Path.Combine(lib, "branding.json");
        File.Copy(sourceManifest, manifestPath);
        File.Copy(Path.Combine(repository, "apps", "web", "src", "app", "favicon.ico"), Path.Combine(app, "favicon.ico"));
        File.Copy(Path.Combine(repository, "apps", "web", "public", "examples", "corporate.webp"), Path.Combine(examples, "corporate.webp"));

        var output = new StringWriter();
        var error = new StringWriter();
        var application = new CliApplication(output, error);
        Assert(await application.RunAsync(["branding", "validate", "--root", root, "--format", "json"]) == 0,
            output + error.ToString());

        var valid = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, valid.Replace("#publications", "javascript:alert(1)", StringComparison.Ordinal));
        output.GetStringBuilder().Clear();
        Assert(await application.RunAsync(["branding", "validate", "--root", root]) == 1,
            "Unsafe navigation should fail branding validation.");
        Assert(output.ToString().Contains("link_unsafe", StringComparison.Ordinal), "Unsafe link failure was not reported.");

        await File.WriteAllTextAsync(manifestPath, valid
            .Replace("\"accent\": \"#087c78\"", "\"accent\": \"#ffffff\"", StringComparison.Ordinal)
            .Replace("\"width\": 1920", "\"width\": 1919", StringComparison.Ordinal));
        var result = await validator.ValidateAsync(root);
        Assert(result.Errors.Any(item => item.Code == "contrast_insufficient"), "Low contrast was not rejected.");
        Assert(result.Errors.Any(item => item.Code == "asset_dimensions_mismatch"), "False asset dimensions were not rejected.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static Task RepositoryBoundaryCheckPasses()
{
    var report = new BoundaryChecker().Run(FindRepositoryRoot());
    Assert(report.IsSuccessful, string.Join("; ", report.Results.Where(result => result.Status == CheckStatus.Failed).Select(result => result.Message)));
    Assert(report.Results.Count >= 4, "Expected all boundary rule families.");
    return Task.CompletedTask;
}

static Task BoundaryCheckDetectsIndustryLeakage()
{
    var root = Path.Combine(Path.GetTempPath(), "opk-cli-tests", Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "OpenPortalKit.Kernel"));
        Directory.CreateDirectory(Path.Combine(root, "src", "OpenPortalKit.ApiHost"));
        Directory.CreateDirectory(Path.Combine(root, "db", "postgresql", "migrations"));
        Directory.CreateDirectory(Path.Combine(root, "tests"));
        File.WriteAllText(Path.Combine(root, "src", "OpenPortalKit.Kernel", "Leak.cs"), "public sealed class Finance { }");
        File.WriteAllText(Path.Combine(root, "src", "OpenPortalKit.ApiHost", "Program.cs"), string.Empty);

        var report = new BoundaryChecker().Run(root);
        Assert(report.Results.Single(result => result.Code == "OPK-BND-001").Status == CheckStatus.Failed,
            "Industry leakage was not detected.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    return Task.CompletedTask;
}

static Task RepositoryAgentContractIsComplete()
{
    var report = new RepositoryAgentReadinessChecker().Run(FindRepositoryRoot());
    Assert(report.IsSuccessful, string.Join("; ", report.Results.Where(result => result.Status == CheckStatus.Failed).Select(result => result.Message)));
    return Task.CompletedTask;
}

static async Task LiveAgentCheckValidatesRepresentations()
{
    using var httpClient = new HttpClient(new AgentReadyHandler());
    var report = await new HttpAgentReadinessChecker(httpClient)
        .RunAsync(new Uri("https://portal.test"));

    Assert(report.IsSuccessful, string.Join("; ", report.Results.Where(result => result.Status == CheckStatus.Failed).Select(result => result.Message)));
    Assert(report.WarningCount == 0, "The complete live fixture should not produce warnings.");
}

static async Task LiveAgentCheckRejectsCrossOriginDiscovery()
{
    using var httpClient = new HttpClient(new AgentReadyHandler(crossOriginContent: true));
    var report = await new HttpAgentReadinessChecker(httpClient)
        .RunAsync(new Uri("https://portal.test"));

    var contentResult = report.Results.Single(result => result.Code == "OPK-AGT-107");
    Assert(contentResult.Status == CheckStatus.Failed, "Cross-origin content discovery should fail closed.");
    Assert(contentResult.Message.Contains("cross-origin", StringComparison.Ordinal), "Failure should explain the origin violation.");
}

static async Task CliValidatesIndustryPack()
{
    var output = new StringWriter();
    var error = new StringWriter();
    var application = new CliApplication(output, error);
    var packPath = Path.Combine(FindRepositoryRoot(), "industry-packs", "Technology");

    var exitCode = await application.RunAsync(["industry-pack", "validate", "--path", packPath, "--format", "json"]);

    Assert(exitCode == 0, error.ToString() + output);
    Assert(output.ToString().Contains("\"Status\": \"passed\"", StringComparison.Ordinal),
        "Pack validation did not produce a passing JSON report.");

    output.GetStringBuilder().Clear();
    var portfolioExitCode = await application.RunAsync([
        "industry-pack", "validate", "--path", Path.Combine(FindRepositoryRoot(), "industry-packs")
    ]);
    Assert(portfolioExitCode == 0, error.ToString() + output);
    Assert(output.ToString().Contains("Finance/pack.json", StringComparison.Ordinal),
        "Portfolio validation did not include every reference pack.");
}

static async Task CliScaffoldsIndustryPack()
{
    var root = CreateTestDirectory();
    try
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var application = new CliApplication(output, error);
        var exitCode = await application.RunAsync([
            "industry-pack", "add", "--name", "Publishing", "--output", root,
            "--display-name", "Publishing Pack", "--description", "Publishing extensions."
        ]);

        var packPath = Path.Combine(root, "Publishing");
        Assert(exitCode == 0 && File.Exists(Path.Combine(packPath, "pack.json")), error.ToString());
        output.GetStringBuilder().Clear();
        Assert(await application.RunAsync(["industry-pack", "validate", "--path", packPath]) == 0,
            error.ToString() + output);
        Assert(await application.RunAsync([
            "industry-pack", "add", "--name", "Publishing", "--output", root
        ]) == 2, "Scaffolding should not overwrite an existing pack.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task CliProducesLegacyDryRunReport()
{
    var root = CreateTestDirectory();
    try
    {
        var csvPath = Path.Combine(root, "legacy.csv");
        var assetsPath = Path.Combine(root, "assets.txt");
        var reportPath = Path.Combine(root, "reports", "dry-run.json");
        await File.WriteAllTextAsync(csvPath, """
            source_id,title,slug,summary,body,old_url,asset_paths
            legacy-1,Welcome,welcome,Summary,Body,/old/welcome,/assets/welcome.pdf
            """);
        await File.WriteAllTextAsync(assetsPath, "# exported asset inventory\n/assets/welcome.pdf\n");
        var output = new StringWriter();
        var error = new StringWriter();
        var application = new CliApplication(output, error);

        var exitCode = await application.RunAsync([
            "import", "legacy", "--input", csvPath, "--output", reportPath,
            "--source", "legacy-mvc", "--import-batch", "batch-20260713",
            "--as-of", "2026-07-12", "--schema-version", "legacy-content.v1",
            "--assets", assetsPath
        ]);

        Assert(exitCode == 0, error.ToString());
        var report = await File.ReadAllTextAsync(reportPath);
        Assert(report.Contains("\"source\": \"legacy-mvc\"", StringComparison.Ordinal), "Source traceability is missing.");
        Assert(report.Contains("\"checksum\":", StringComparison.Ordinal), "Checksum traceability is missing.");
        Assert(report.Contains("\"canApply\": true", StringComparison.Ordinal), "Apply decision is missing.");
        Assert(await application.RunAsync([
            "import", "legacy", "--input", csvPath, "--output", reportPath,
            "--source", "legacy-mvc", "--import-batch", "batch-20260713",
            "--as-of", "2026-07-12", "--schema-version", "legacy-content.v1"
        ]) == 2, "The CLI should not overwrite an existing report.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task CliBlocksUnsafeLegacyRows()
{
    var root = CreateTestDirectory();
    try
    {
        var csvPath = Path.Combine(root, "unsafe.csv");
        var reportPath = Path.Combine(root, "unsafe-report.json");
        await File.WriteAllTextAsync(csvPath, """
            source_id,title,slug,summary,body,old_url,asset_paths
            legacy-1,Welcome,Not Canonical,Summary,Body,,/assets/missing.pdf
            """);
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await new CliApplication(output, error).RunAsync([
            "import", "legacy", "--input", csvPath, "--output", reportPath,
            "--source", "legacy", "--import-batch", "unsafe", "--as-of", "2026-07-12",
            "--schema-version", "legacy-content.v1"
        ]);

        Assert(exitCode == 1 && File.Exists(reportPath), "Unsafe rows should produce a failed report.");
        var report = await File.ReadAllTextAsync(reportPath);
        Assert(report.Contains("slug_not_canonical", StringComparison.Ordinal) &&
               report.Contains("asset_missing", StringComparison.Ordinal), "Expected migration issues are missing.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task CliCreatesTraceableSourceWorkspace()
{
    var root = CreateTestDirectory();
    try
    {
        var source = CreateWorkspaceTemplate(Path.Combine(root, "template"));
        var outputPath = Path.Combine(root, "generated");
        var output = new StringWriter();
        var error = new StringWriter();
        var application = new CliApplication(output, error);

        var exitCode = await application.RunAsync([
            "new", "--name", "Atlas Investor Information", "--profile", "finance",
            "--output", outputPath, "--source", source
        ]);

        Assert(exitCode == 0, error.ToString());
        Assert(File.Exists(Path.Combine(outputPath, "OpenPortalKit.sln")), "Solution was not copied.");
        Assert(File.Exists(Path.Combine(outputPath, "docker", ".env.example")), "Safe environment example was not copied.");
        Assert(!File.Exists(Path.Combine(outputPath, "docker", ".env")), "Environment secrets were copied.");
        Assert(!Directory.Exists(Path.Combine(outputPath, "src", "bin")), "Build outputs were copied.");
        Assert(!Directory.Exists(Path.Combine(outputPath, "apps", "web", "node_modules")), "Node dependencies were copied.");

        using var project = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(outputPath, "openportalkit.project.json")));
        var projectRoot = project.RootElement;
        Assert(projectRoot.GetProperty("schemaVersion").GetString() == "opk.project.v2", "Project schema is missing.");
        Assert(projectRoot.GetProperty("profile").GetProperty("id").GetString() == "finance",
            "Versioned project profile id is missing.");
        Assert(projectRoot.GetProperty("profile").GetProperty("version").GetString() == "1.0.0",
            "Versioned project profile version is missing.");
        Assert(projectRoot.GetProperty("profile").GetProperty("checksum").GetString()?.Length == 64,
            "Versioned project profile checksum is missing.");
        Assert(projectRoot.GetProperty("source").GetProperty("checksum").GetString()?.Length == 64,
            "Template checksum is missing.");
        Assert(projectRoot.GetProperty("selectedIndustryPacks")[0].GetString() == "Finance",
            "Finance selection was not kept outside core configuration.");

        using var profile = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(outputPath, "apps", "web", "src", "lib", "project-profile.json")));
        Assert(profile.RootElement.GetProperty("projectName").GetString() == "Atlas Investor Information",
            "Generated Web branding is incorrect.");
        Assert(profile.RootElement.GetProperty("defaultSite").GetString() == "finance",
            "Generated Web profile is incorrect.");
        Assert(profile.RootElement.GetProperty("profileTemplate").GetProperty("version").GetString() == "1.0.0",
            "Generated Web profile provenance is missing.");
        using var branding = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(outputPath, "apps", "web", "src", "lib", "branding.json")));
        Assert(branding.RootElement.GetProperty("schemaVersion").GetString() == "opk.branding.v1",
            "Generated branding schema is missing.");
        Assert(branding.RootElement.GetProperty("site").GetProperty("name").GetString() == "Atlas Investor Information",
            "Generated branding identity is incorrect.");
        Assert(branding.RootElement.GetProperty("site").GetProperty("shortName").GetString() == "AII",
            "Generated branding fallback mark is incorrect.");
        Assert(branding.RootElement.GetProperty("typography").GetProperty("preset").GetString() == "institutional",
            "Generated profile typography is incorrect.");
        Assert(branding.RootElement.GetProperty("assets").GetProperty("socialImage").GetProperty("src").GetString() == "/examples/finance.webp",
            "Generated profile social asset is incorrect.");
        Assert(await application.RunAsync([
            "new", "--name", "Duplicate", "--output", outputPath, "--source", source
        ]) == 2, "Existing output should not be overwritten.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task WorkspaceScaffoldingRejectsUnsafePaths()
{
    var root = CreateTestDirectory();
    try
    {
        var source = CreateWorkspaceTemplate(Path.Combine(root, "template"));
        var application = new CliApplication(new StringWriter(), new StringWriter());
        Assert(await application.RunAsync([
            "new", "--name", "Nested Portal", "--output", Path.Combine(source, "generated"), "--source", source
        ]) == 2, "Output nested in the source should be rejected.");
        Assert(await application.RunAsync([
            "new", "--name", "Bad Profile", "--profile", "trading", "--output", Path.Combine(root, "bad"), "--source", source
        ]) == 2, "Unknown profiles should be rejected.");

        if (!OperatingSystem.IsWindows())
        {
            var outside = Path.Combine(root, "outside.txt");
            await File.WriteAllTextAsync(outside, "outside");
            File.CreateSymbolicLink(Path.Combine(source, "src", "linked.txt"), outside);
            Assert(await application.RunAsync([
                "new", "--name", "Linked Portal", "--output", Path.Combine(root, "linked"), "--source", source
            ]) == 2, "Symbolic links in the template should be rejected.");
            Assert(!Directory.Exists(Path.Combine(root, "linked")), "Failed scaffolding left a partial workspace.");
        }
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task ProjectProfilesRejectInvalidManifests()
{
    var root = CreateTestDirectory();
    try
    {
        var source = CreateWorkspaceTemplate(Path.Combine(root, "template"));
        var profilePath = Path.Combine(source, "templates", "project-profiles", "finance.json");
        var catalog = new OpenPortalKit.Cli.Authoring.ProjectProfileCatalog();

        await File.WriteAllTextAsync(profilePath, """
            {
              "schemaVersion": "opk.project-template-profile.v1",
              "id": "finance",
              "version": "1.0.0",
              "defaultSite": "finance",
              "selectedIndustryPacks": ["Finance"],
              "unexpected": true
            }
            """);
        await AssertThrowsAsync<FormatException>(() => catalog.LoadAsync(source, "finance"),
            "Unknown profile properties should fail closed.");

        await File.WriteAllTextAsync(profilePath, ProjectProfile("finance", "MissingPack"));
        await AssertThrowsAsync<FormatException>(() => catalog.LoadAsync(source, "finance"),
            "Missing industry pack references should fail closed.");

        await File.WriteAllTextAsync(profilePath, ProjectProfile("finance").Replace("1.0.0", "v1"));
        await AssertThrowsAsync<FormatException>(() => catalog.LoadAsync(source, "finance"),
            "Invalid profile semantic versions should fail closed.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task OfflineTemplateArchivesAreVerified()
{
    var root = CreateTestDirectory();
    try
    {
        var source = CreateWorkspaceTemplate(Path.Combine(root, "template"));
        var archivePath = Path.Combine(root, "release.opkt");
        var archive = new OpenPortalKit.Cli.Authoring.TemplateArchive();
        var packed = await archive.PackAsync(source, archivePath);
        Assert(packed.FileCount > 5 && packed.SourceChecksum.Length == 64 && packed.ArchiveChecksum.Length == 64,
            "Template archive provenance is incomplete.");

        var outputPath = Path.Combine(root, "offline-workspace");
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await new CliApplication(output, error).RunAsync([
            "new", "--name", "Offline Portal", "--profile", "finance",
            "--output", outputPath, "--source", archivePath
        ]);
        Assert(exitCode == 0, error.ToString());
        using var project = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(outputPath, "openportalkit.project.json")));
        Assert(project.RootElement.GetProperty("source").GetProperty("checksum").GetString() == packed.SourceChecksum,
            "Offline generation did not preserve archive source provenance.");

        var tamperedPath = Path.Combine(root, "tampered.opkt");
        File.Copy(archivePath, tamperedPath);
        using (var zip = ZipFile.Open(tamperedPath, ZipArchiveMode.Update))
        {
            zip.GetEntry("opk-template.json")!.Delete();
            var unsafeEntry = zip.CreateEntry("../escape.txt");
            await using (var writer = new StreamWriter(unsafeEntry.Open())) await writer.WriteAsync("escape");
            var manifestEntry = zip.CreateEntry("opk-template.json");
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, new
            {
                schemaVersion = "opk.source-template-archive.v1",
                templateVersion = OpenPortalKit.Cli.Authoring.WorkspaceScaffolder.TemplateVersion,
                createdAt = DateTimeOffset.UtcNow,
                sourceChecksum = packed.SourceChecksum,
                fileCount = packed.FileCount + 1
            });
        }
        await AssertThrowsAsync<FormatException>(() => archive.ExtractAsync(tamperedPath),
            "Archive path traversal should fail closed.");
        Assert(!File.Exists(Path.Combine(root, "escape.txt")), "Unsafe archive entry escaped extraction root.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task RepositoryWorkspaceScaffoldPreservesBoundaries()
{
    var outputPath = Path.Combine(Path.GetTempPath(), "opk-cli-tests", Guid.NewGuid().ToString("N"), "workspace");
    try
    {
        var result = await new OpenPortalKit.Cli.Authoring.WorkspaceScaffolder().CreateAsync(
            new OpenPortalKit.Cli.Authoring.WorkspaceScaffoldOptions(
                "Boundary Fixture", "corporate", outputPath, FindRepositoryRoot()));
        Assert(result.FileCount > 400 && result.SourceChecksum.Length == 64,
            "Repository workspace inventory is incomplete.");
        var output = new StringWriter();
        var error = new StringWriter();
        var application = new CliApplication(output, error);
        var addExitCode = await application.RunAsync([
            "module", "add", "--name", "Announcements", "--area", "publishing-support",
            "--description", "Coordinates reusable announcement delivery contracts.",
            "--owns-state", "false", "--public-outputs", "JSON,Markdown", "--root", outputPath
        ]);
        Assert(addExitCode == 0, error.ToString());
        var moduleRoot = Path.Combine(outputPath, "src", "OpenPortalKit.Modules.Announcements");
        var testRoot = Path.Combine(outputPath, "tests", "OpenPortalKit.Modules.Announcements.Tests");
        Assert(File.Exists(Path.Combine(moduleRoot, "AnnouncementsModule.cs")), "Module descriptor was not generated.");
        Assert(File.Exists(Path.Combine(testRoot, "Program.cs")), "Module contract tests were not generated.");
        var descriptor = await File.ReadAllTextAsync(Path.Combine(moduleRoot, "AnnouncementsModule.cs"));
        Assert(descriptor.Contains("new[] { \"JSON\", \"Markdown\" }", StringComparison.Ordinal),
            "Public outputs were not normalized deterministically.");
        var solution = await File.ReadAllTextAsync(Path.Combine(outputPath, "OpenPortalKit.sln"));
        Assert(solution.Contains("OpenPortalKit.Modules.Announcements.Tests", StringComparison.Ordinal),
            "Generated projects were not registered in the solution.");
        Assert(await application.RunAsync([
            "module", "add", "--name", "Announcements", "--area", "publishing-support",
            "--description", "Duplicate module.", "--root", outputPath
        ]) == 2, "Existing modules should not be overwritten.");

        var solutionBeforeRejectedModule = await File.ReadAllBytesAsync(Path.Combine(outputPath, "OpenPortalKit.sln"));
        Assert(await application.RunAsync([
            "module", "add", "--name", "Finance", "--area", "publishing-support",
            "--description", "Must remain in an industry pack.", "--root", outputPath
        ]) == 2, "Industry-specific core modules should be rejected.");
        Assert(!Directory.Exists(Path.Combine(outputPath, "src", "OpenPortalKit.Modules.Finance")),
            "Rejected module source was not rolled back.");
        var solutionAfterRejectedModule = await File.ReadAllBytesAsync(Path.Combine(outputPath, "OpenPortalKit.sln"));
        Assert(solutionBeforeRejectedModule.SequenceEqual(solutionAfterRejectedModule),
            "Rejected module changed the solution file.");

        var report = new BoundaryChecker().Run(outputPath);
        Assert(report.IsSuccessful,
            string.Join("; ", report.Results.Where(item => item.Status == CheckStatus.Failed).Select(item => item.Message)));

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        Assert(await application.RunAsync([
            "upgrade", "inspect", "--root", outputPath, "--source", FindRepositoryRoot(), "--format", "json"
        ]) == 0, error.ToString() + output);
        Assert(output.ToString().Contains("\"Warnings\": 0", StringComparison.Ordinal),
            "Unchanged source should produce a clean upgrade inspection.");

        var projectManifestPath = Path.Combine(outputPath, "openportalkit.project.json");
        var projectManifest = await File.ReadAllTextAsync(projectManifestPath);
        projectManifest = projectManifest.Replace(
            result.SourceChecksum,
            new string('0', 64),
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(projectManifestPath, projectManifest);
        output.GetStringBuilder().Clear();
        Assert(await application.RunAsync([
            "upgrade", "inspect", "--root", outputPath, "--source", FindRepositoryRoot()
        ]) == 0, "Source drift should be reported as a non-destructive warning.");
        Assert(output.ToString().Contains("WARN OPK-UPG-003", StringComparison.Ordinal),
            "Source provenance drift warning is missing.");
    }
    finally
    {
        var parent = Directory.GetParent(outputPath)?.FullName;
        if (parent is not null && Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }
}

static string CreateWorkspaceTemplate(string root)
{
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(Path.Combine(root, "src", "bin"));
    Directory.CreateDirectory(Path.Combine(root, "apps", "web", "src", "lib"));
    Directory.CreateDirectory(Path.Combine(root, "apps", "web", "node_modules", "package"));
    Directory.CreateDirectory(Path.Combine(root, "db"));
    Directory.CreateDirectory(Path.Combine(root, "tools"));
    Directory.CreateDirectory(Path.Combine(root, "docker"));
    Directory.CreateDirectory(Path.Combine(root, "templates", "project-profiles"));
    Directory.CreateDirectory(Path.Combine(root, "industry-packs", "Finance"));
    File.WriteAllText(Path.Combine(root, "OpenPortalKit.sln"), "solution");
    File.WriteAllText(Path.Combine(root, "README.md"), "template readme");
    File.WriteAllText(Path.Combine(root, "src", "Keep.cs"), "public class Keep { }");
    File.WriteAllText(Path.Combine(root, "db", "README.md"), "database assets\n");
    File.WriteAllText(Path.Combine(root, "src", "bin", "ignored.dll"), "ignored");
    File.WriteAllText(Path.Combine(root, "apps", "web", "node_modules", "package", "ignored.js"), "ignored");
    File.WriteAllText(Path.Combine(root, "docker", ".env"), "SECRET=do-not-copy");
    File.WriteAllText(Path.Combine(root, "docker", ".env.example"), "SECRET=<set-me>");
    File.WriteAllText(Path.Combine(root, "apps", "web", "src", "lib", "project-profile.json"), "{}\n");
    File.WriteAllText(Path.Combine(root, "tools", "opk"), "#!/usr/bin/env bash\n");
    File.WriteAllText(Path.Combine(root, "industry-packs", "Finance", "pack.json"), "{}\n");
    File.WriteAllText(Path.Combine(root, "templates", "project-profiles", "corporate.json"), ProjectProfile("corporate"));
    File.WriteAllText(Path.Combine(root, "templates", "project-profiles", "finance.json"), ProjectProfile("finance", "Finance"));
    return root;
}

static string ProjectProfile(string id, params string[] packs) => JsonSerializer.Serialize(new
{
    schemaVersion = "opk.project-template-profile.v1",
    id,
    version = "1.0.0",
    defaultSite = id,
    selectedIndustryPacks = packs
});

static string CreateTestDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "opk-cli-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "OpenPortalKit.sln"))) return current.FullName;
        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

file sealed class AgentReadyHandler(bool crossOriginContent = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var response = path switch
        {
            "/robots.txt" => Response("text/plain", "User-agent: *\nSitemap: https://portal.test/sitemap.xml"),
            "/sitemap.xml" => Response("application/xml", "<urlset><url><loc>https://portal.test/content/welcome</loc></url></urlset>"),
            "/rss.xml" => Response("application/rss+xml", "<rss version=\"2.0\"><channel /></rss>"),
            "/llms.txt" => Response("text/plain", "# Portal"),
            "/.well-known/agent.json" => Response("application/json", "{\"name\":\"Portal\"}"),
            "/api/openapi.json" => Response("application/json", "{\"openapi\":\"3.1.0\"}"),
            "/api/public/content" => Response("application/json", crossOriginContent
                ? """{"items":[{"canonicalUrl":"http://127.0.0.1/private","markdownSnapshot":"https://portal.test/content/welcome.md","jsonSnapshot":"https://portal.test/api/public/content/welcome.json"}]}"""
                : """{"items":[{"canonicalUrl":"https://portal.test/content/welcome","markdownSnapshot":"https://portal.test/content/welcome.md","jsonSnapshot":"https://portal.test/api/public/content/welcome.json"}]}"""),
            "/content/welcome" => Response("text/html", "<html><head><link rel=\"canonical\"><script type=\"application/ld+json\">{}</script></head><body><main>Welcome</main></body></html>"),
            "/content/welcome.md" => Response("text/markdown", "# Welcome"),
            "/api/public/content/welcome.json" => Response("application/json", "{\"title\":\"Welcome\"}"),
            "/api/public/datasets" => Response("application/json", "[{\"code\":\"sample\"}]"),
            "/api/public/datasets/sample/schema" => Response("application/json", "{\"version\":\"1\"}"),
            "/api/public/datasets/sample/records" => Response("application/json", "{\"items\":[]}"),
            "/api/public/datasets/sample/export.csv" => Response("text/csv", "id\n1"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
        if (path.StartsWith("/api/", StringComparison.Ordinal))
            response.Headers.TryAddWithoutValidation("X-OpenPortalKit-Contract-Version", "1.0.0");
        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    private static HttpResponseMessage Response(string mediaType, string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, mediaType)
    };
}
