using OpenPortalKit.Cli.Checks;
using OpenPortalKit.Cli.Authoring;
using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.Cli;

public sealed class CliApplication(TextWriter output, TextWriter error)
{
    private const string Version = "0.5.0-r15";
    private readonly TextWriter _output = output ?? throw new ArgumentNullException(nameof(output));
    private readonly TextWriter _error = error ?? throw new ArgumentNullException(nameof(error));

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            WriteHelp();
            return 0;
        }

        if (args[0] is "--version" or "-v")
        {
            await _output.WriteLineAsync(Version);
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "check-boundaries" => RunBoundaryCheck(args[1..]),
                "check-agent-readiness" => await RunAgentReadinessCheckAsync(args[1..], cancellationToken),
                "new" => await RunNewAsync(args[1..], cancellationToken),
                "module" => await RunModuleAsync(args[1..], cancellationToken),
                "template" => await RunTemplateAsync(args[1..], cancellationToken),
                "upgrade" => await RunUpgradeAsync(args[1..], cancellationToken),
                "branding" => await RunBrandingAsync(args[1..], cancellationToken),
                "industry-pack" => await RunIndustryPackAsync(args[1..], cancellationToken),
                "import" => await RunImportAsync(args[1..], cancellationToken),
                _ => UsageError($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or
            FileNotFoundException or FormatException or IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception)
        {
            await _error.WriteLineAsync("error: " + exception.Message);
            return 2;
        }
    }

    private async Task<int> RunUpgradeAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] != "inspect")
            return UsageError("upgrade requires the 'inspect' subcommand.");
        var options = ParseUpgradeInspectOptions(args[1..]);
        var report = await new UpgradeInspector().InspectAsync(
            Path.GetFullPath(options.Root),
            RepositoryLocator.Find(options.Source),
            cancellationToken);
        WriteReport(report, options.Format);
        return report.IsSuccessful ? 0 : 1;
    }

    private async Task<int> RunBrandingAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] != "validate")
            return UsageError("branding requires the 'validate' subcommand.");
        var options = ParseOptions(args[1..], allowUrl: false, allowTimeout: false);
        var root = RepositoryLocator.Find(options.Root);
        var result = await new BrandingManifestValidator().ValidateAsync(root, cancellationToken);
        CheckReport report;
        if (result.Succeeded)
        {
            report = new CheckReport("OpenPortalKit branding validation",
            [
                new CheckResult("OPK-BRAND-001", CheckStatus.Passed, BrandingManifestValidator.RelativeManifestPath,
                    $"Manifest uses {BrandingManifestValidator.SchemaVersion} and passed semantic validation."),
                new CheckResult("OPK-BRAND-002", CheckStatus.Passed, "Web branding assets",
                    $"{result.AssetCount} declared assets exist, are contained, and match their declared dimensions.")
            ]);
        }
        else
        {
            report = new CheckReport("OpenPortalKit branding validation",
                result.Errors.Select(validationError => new CheckResult(
                    "OPK-BRAND-VALIDATION",
                    CheckStatus.Failed,
                    validationError.Path,
                    $"{validationError.Code}: {validationError.Message}"))
                    .ToArray());
        }

        WriteReport(report, options.Format);
        return report.IsSuccessful ? 0 : 1;
    }

    private async Task<int> RunModuleAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] != "add")
            return UsageError("module requires the 'add' subcommand.");

        var options = ParseModuleAddOptions(args[1..]);
        var root = RepositoryLocator.Find(options.Root);
        var result = await new ModuleScaffolder().CreateAsync(
            new ModuleScaffoldOptions(
                options.Name,
                options.Area,
                options.Description,
                options.OwnsBusinessState,
                options.PublicOutputs,
                root),
            cancellationToken);
        await _output.WriteLineAsync($"Created module '{result.Name}' at {result.SourcePath}");
        await _output.WriteLineAsync($"Created contract tests at {result.TestPath}");
        await _output.WriteLineAsync("The module depends on Kernel only and is registered in OpenPortalKit.sln.");
        return 0;
    }

    private async Task<int> RunNewAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = ParseNewOptions(args);
        TemplateArchiveExtraction? extraction = null;
        var sourceRoot = options.SourceRoot is not null && File.Exists(options.SourceRoot)
            ? (extraction = await new TemplateArchive().ExtractAsync(options.SourceRoot, cancellationToken)).Path
            : RepositoryLocator.Find(options.SourceRoot);
        await using var extractionLease = extraction;
        var result = await new WorkspaceScaffolder().CreateAsync(
            new WorkspaceScaffoldOptions(options.Name, options.Profile, options.OutputPath, sourceRoot),
            cancellationToken);
        await _output.WriteLineAsync($"Created '{result.Name}' from the {result.Profile} profile at {result.Path}");
        await _output.WriteLineAsync($"Template {WorkspaceScaffolder.TemplateVersion}, {result.FileCount} files, checksum {result.SourceChecksum}");
        await _output.WriteLineAsync($"Next: cd \"{result.Path}\" and run 'dotnet build OpenPortalKit.sln -m:1'.");
        return 0;
    }

    private async Task<int> RunTemplateAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] != "pack")
            return UsageError("template requires the 'pack' subcommand.");
        string? source = null;
        string? output = null;
        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--source": source = RequireValue(option, value); index++; break;
                case "--output": output = RequireValue(option, value); index++; break;
                default: throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }
        if (string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("template pack requires --output <archive.opkt>.");
        var result = await new TemplateArchive().PackAsync(RepositoryLocator.Find(source), output, cancellationToken);
        await _output.WriteLineAsync($"Created source template archive at {result.Path}");
        await _output.WriteLineAsync($"Source {result.FileCount} files, checksum {result.SourceChecksum}");
        await _output.WriteLineAsync($"Archive checksum {result.ArchiveChecksum}");
        return 0;
    }

    private int RunBoundaryCheck(string[] args)
    {
        var options = ParseOptions(args, allowUrl: false, allowTimeout: false);
        var root = RepositoryLocator.Find(options.Root);
        var report = new BoundaryChecker().Run(root);
        WriteReport(report, options.Format);
        return report.IsSuccessful ? 0 : 1;
    }

    private async Task<int> RunAgentReadinessCheckAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = ParseOptions(args, allowUrl: true, allowTimeout: true);
        CheckReport report;

        if (options.Url is null)
        {
            var root = RepositoryLocator.Find(options.Root);
            report = new RepositoryAgentReadinessChecker().Run(root);
        }
        else
        {
            if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var siteUrl) ||
                siteUrl.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException("--url must be an absolute HTTP or HTTPS URL.");
            }

            using var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false
            };
            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                MaxResponseContentBufferSize = 2 * 1024 * 1024
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenPortalKit-AgentReadiness/" + Version);
            report = await new HttpAgentReadinessChecker(httpClient).RunAsync(siteUrl, cancellationToken);
        }

        WriteReport(report, options.Format);
        return report.IsSuccessful ? 0 : 1;
    }

    private async Task<int> RunIndustryPackAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
            return UsageError("industry-pack requires the 'add' or 'validate' subcommand.");

        if (args[0] == "add")
        {
            var addOptions = ParseIndustryPackAddOptions(args[1..]);
            var result = await new IndustryPackScaffolder().CreateAsync(addOptions, cancellationToken);
            await _output.WriteLineAsync($"Created industry pack '{result.Name}' at {result.Path}");
            await _output.WriteLineAsync("Next: edit content-types/catalog.json, then run 'opk industry-pack validate --path \"" + result.Path + "\"'.");
            return 0;
        }

        if (args[0] != "validate")
            return UsageError("industry-pack requires the 'add' or 'validate' subcommand.");

        var options = ParseIndustryPackOptions(args[1..]);
        var packPath = Path.GetFullPath(options.Path);
        CheckReport report;
        var loader = new IndustryPackLoader(options.CoreVersion);
        if (File.Exists(Path.Combine(packPath, "pack.json")))
        {
            var result = await loader.LoadAsync(packPath, cancellationToken);
            report = result.Succeeded
                ? BuildPackReport([result.Pack!])
                : BuildPackErrorReport(result.Errors);
        }
        else
        {
            var result = await new IndustryPackCatalog(loader).DiscoverAsync(packPath, cancellationToken);
            report = result.Succeeded
                ? BuildPackReport(result.Packs)
                : BuildPackErrorReport(result.Errors);
        }

        WriteReport(report, options.Format);
        return report.IsSuccessful ? 0 : 1;
    }

    private async Task<int> RunImportAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] != "legacy")
            return UsageError("import requires the 'legacy' subcommand.");

        var options = ParseLegacyImportOptions(args[1..]);
        var result = await new LegacyImportRunner().AnalyzeAsync(options, cancellationToken);
        await _output.WriteLineAsync(
            $"Legacy migration dry run: {result.Report.ValidRows}/{result.Report.TotalRows} rows valid, " +
            $"{result.ErrorCount} errors, {result.WarningCount} warnings.");
        await _output.WriteLineAsync($"Traceable report: {result.OutputPath}");
        return result.Report.CanApply ? 0 : 1;
    }

    private static CheckReport BuildPackReport(IReadOnlyList<LoadedIndustryPack> packs)
    {
        var results = packs.OrderBy(pack => pack.Manifest.Name, StringComparer.Ordinal)
            .SelectMany(pack => new[]
            {
                new CheckResult(
                    "OPK-PACK-001",
                    CheckStatus.Passed,
                    pack.Manifest.Name + "/pack.json",
                    $"Manifest {pack.Manifest.ManifestVersion}, pack version {pack.Manifest.Version}, requires core {pack.Manifest.RequiresCore}."),
                new CheckResult(
                    "OPK-PACK-002",
                    CheckStatus.Passed,
                    pack.Manifest.Name + "/resources",
                    $"{pack.Resources.Count} declared resources are contained, valid JSON, and checksummed.")
            })
            .ToArray();
        return new CheckReport("OpenPortalKit industry pack validation", results);
    }

    private static CheckReport BuildPackErrorReport(IReadOnlyList<IndustryPackValidationError> errors) =>
        new(
            "OpenPortalKit industry pack validation",
            errors.Select(error => new CheckResult(
                "OPK-PACK-VALIDATION",
                CheckStatus.Failed,
                error.ResourcePath ?? "pack",
                $"{error.Code}: {error.Message}"))
                .ToArray());

    private void WriteReport(CheckReport report, string format)
    {
        if (format == "json") CheckReportWriter.WriteJson(_output, report);
        else CheckReportWriter.WriteText(_output, report);
    }

    private static CliOptions ParseOptions(string[] args, bool allowUrl, bool allowTimeout)
    {
        string? root = null;
        string? url = null;
        var format = "text";
        var timeoutSeconds = 10;

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--root":
                    root = RequireValue(option, value);
                    index++;
                    break;
                case "--format":
                    format = RequireValue(option, value).ToLowerInvariant();
                    if (format is not ("text" or "json"))
                        throw new ArgumentException("--format must be 'text' or 'json'.");
                    index++;
                    break;
                case "--url" when allowUrl:
                    url = RequireValue(option, value);
                    index++;
                    break;
                case "--timeout" when allowTimeout:
                    if (!int.TryParse(RequireValue(option, value), out timeoutSeconds) || timeoutSeconds is < 1 or > 120)
                        throw new ArgumentException("--timeout must be between 1 and 120 seconds.");
                    index++;
                    break;
                default:
                    throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }

        if (url is not null && root is not null)
            throw new ArgumentException("--root and --url select different check modes and cannot be combined.");

        return new CliOptions(root, url, format, timeoutSeconds);
    }

    private static IndustryPackCliOptions ParseIndustryPackOptions(string[] args)
    {
        string? path = null;
        var coreVersion = IndustryPackContract.CurrentCoreVersion;
        var format = "text";

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--path":
                    path = RequireValue(option, value);
                    index++;
                    break;
                case "--core-version":
                    coreVersion = RequireValue(option, value);
                    index++;
                    break;
                case "--format":
                    format = RequireValue(option, value).ToLowerInvariant();
                    if (format is not ("text" or "json"))
                        throw new ArgumentException("--format must be 'text' or 'json'.");
                    index++;
                    break;
                default:
                    throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("industry-pack validate requires --path <directory>.");

        return new IndustryPackCliOptions(path, coreVersion, format);
    }

    private static NewWorkspaceCliOptions ParseNewOptions(string[] args)
    {
        string? name = null;
        string? output = null;
        string? source = null;
        var profile = "corporate";

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--name": name = RequireValue(option, value); index++; break;
                case "--output": output = RequireValue(option, value); index++; break;
                case "--profile": profile = RequireValue(option, value); index++; break;
                case "--source": source = RequireValue(option, value); index++; break;
                default: throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("new requires --name <display-name> and --output <directory>.");
        return new NewWorkspaceCliOptions(name, output, profile, source);
    }

    private static IndustryPackScaffoldOptions ParseIndustryPackAddOptions(string[] args)
    {
        string? name = null;
        string? displayName = null;
        string? description = null;
        string? output = null;

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--name": name = RequireValue(option, value); index++; break;
                case "--display-name": displayName = RequireValue(option, value); index++; break;
                case "--description": description = RequireValue(option, value); index++; break;
                case "--output": output = RequireValue(option, value); index++; break;
                default: throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("industry-pack add requires --name <PascalCaseName> and --output <directory>.");

        return new IndustryPackScaffoldOptions(
            name,
            displayName ?? name + " Pack",
            description ?? $"Optional {name} publishing extensions for OpenPortalKit.",
            output);
    }

    private static ModuleAddCliOptions ParseModuleAddOptions(string[] args)
    {
        string? name = null;
        string? area = null;
        string? description = null;
        string? root = null;
        var ownsBusinessState = false;
        IReadOnlyList<string> publicOutputs = Array.Empty<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--name": name = RequireValue(option, value); index++; break;
                case "--area": area = RequireValue(option, value); index++; break;
                case "--description": description = RequireValue(option, value); index++; break;
                case "--root": root = RequireValue(option, value); index++; break;
                case "--owns-state":
                    if (!bool.TryParse(RequireValue(option, value), out ownsBusinessState))
                        throw new ArgumentException("--owns-state must be 'true' or 'false'.");
                    index++;
                    break;
                case "--public-outputs":
                    var outputValue = RequireValue(option, value);
                    publicOutputs = outputValue.Equals("none", StringComparison.OrdinalIgnoreCase)
                        ? Array.Empty<string>()
                        : outputValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    index++;
                    break;
                default: throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }

        if (new[] { name, area, description }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("module add requires --name, --area, and --description.");
        return new ModuleAddCliOptions(name!, area!, description!, ownsBusinessState, publicOutputs, root);
    }

    private static UpgradeInspectCliOptions ParseUpgradeInspectOptions(string[] args)
    {
        string? root = null;
        string? source = null;
        var format = "text";
        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--root": root = RequireValue(option, value); index++; break;
                case "--source": source = RequireValue(option, value); index++; break;
                case "--format":
                    format = RequireValue(option, value).ToLowerInvariant();
                    if (format is not ("text" or "json"))
                        throw new ArgumentException("--format must be 'text' or 'json'.");
                    index++;
                    break;
                default: throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("upgrade inspect requires --root <workspace> and --source <candidate-repository>.");
        return new UpgradeInspectCliOptions(root, source, format);
    }

    private static LegacyImportOptions ParseLegacyImportOptions(string[] args)
    {
        string? input = null;
        string? output = null;
        string? source = null;
        string? importBatch = null;
        string? schemaVersion = null;
        string? assets = null;
        DateOnly? asOfDate = null;

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;
            switch (option)
            {
                case "--input": input = RequireValue(option, value); index++; break;
                case "--output": output = RequireValue(option, value); index++; break;
                case "--source": source = RequireValue(option, value); index++; break;
                case "--import-batch": importBatch = RequireValue(option, value); index++; break;
                case "--schema-version": schemaVersion = RequireValue(option, value); index++; break;
                case "--assets": assets = RequireValue(option, value); index++; break;
                case "--as-of":
                    if (!DateOnly.TryParseExact(RequireValue(option, value), "yyyy-MM-dd", out var parsedDate))
                        throw new ArgumentException("--as-of must use yyyy-MM-dd format.");
                    asOfDate = parsedDate;
                    index++;
                    break;
                default: throw new ArgumentException($"Unknown or unsupported option '{option}'.");
            }
        }

        if (new[] { input, output, source, importBatch, schemaVersion }.Any(string.IsNullOrWhiteSpace) || asOfDate is null)
            throw new ArgumentException("import legacy requires --input, --output, --source, --import-batch, --as-of, and --schema-version.");

        return new LegacyImportOptions(input!, output!, source!, importBatch!, asOfDate.Value, schemaVersion!, assets);
    }

    private static string RequireValue(string option, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
            throw new ArgumentException($"{option} requires a value.");
        return value;
    }

    private int UsageError(string message)
    {
        _error.WriteLine("error: " + message);
        _error.WriteLine("Run 'opk --help' for usage.");
        return 2;
    }

    private void WriteHelp()
    {
        _output.WriteLine("OpenPortalKit developer CLI");
        _output.WriteLine();
        _output.WriteLine("Usage:");
        _output.WriteLine("  opk new --name <display-name> --output <directory> [--profile corporate|data|research|activity|finance] [--source <repository>]");
        _output.WriteLine("  opk module add --name <PascalCaseName> --area <slug> --description <text> [--owns-state true|false] [--public-outputs HTML,Markdown,JSON,Sitemap,RSS,Search,AgentSEO|none] [--root <repository>]");
        _output.WriteLine("  opk template pack --output <archive.opkt> [--source <repository>]");
        _output.WriteLine("  opk upgrade inspect --root <workspace> --source <candidate-repository> [--format text|json]");
        _output.WriteLine("  opk branding validate [--root <workspace>] [--format text|json]");
        _output.WriteLine("  opk check-boundaries [--root <path>] [--format text|json]");
        _output.WriteLine("  opk check-agent-readiness [--root <path>] [--format text|json]");
        _output.WriteLine("  opk check-agent-readiness --url <https://site> [--timeout <seconds>] [--format text|json]");
        _output.WriteLine("  opk industry-pack add --name <PascalCaseName> --output <directory> [--display-name <name>] [--description <text>]");
        _output.WriteLine("  opk industry-pack validate --path <directory> [--core-version <version>] [--format text|json]");
        _output.WriteLine("  opk import legacy --input <csv> --output <report.json> --source <name> --import-batch <id> --as-of <yyyy-MM-dd> --schema-version <version> [--assets <inventory.txt>]");
        _output.WriteLine("  opk --version");
        _output.WriteLine();
        _output.WriteLine("Exit codes: 0 passed, 1 checks failed, 2 invalid usage.");
    }

    private sealed record CliOptions(
        string? Root,
        string? Url,
        string Format,
        int TimeoutSeconds);

    private sealed record NewWorkspaceCliOptions(
        string Name,
        string OutputPath,
        string Profile,
        string? SourceRoot);

    private sealed record ModuleAddCliOptions(
        string Name,
        string Area,
        string Description,
        bool OwnsBusinessState,
        IReadOnlyList<string> PublicOutputs,
        string? Root);

    private sealed record UpgradeInspectCliOptions(
        string Root,
        string Source,
        string Format);

    private sealed record IndustryPackCliOptions(
        string Path,
        string CoreVersion,
        string Format);
}
