using OpenPortalKit.Cli.Checks;
using OpenPortalKit.Cli.Authoring;
using OpenPortalKit.Modules.IndustryPacks;

namespace OpenPortalKit.Cli;

public sealed class CliApplication(TextWriter output, TextWriter error)
{
    private const string Version = "0.1.0-r12";
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
                "industry-pack" => await RunIndustryPackAsync(args[1..], cancellationToken),
                "import" => await RunImportAsync(args[1..], cancellationToken),
                _ => UsageError($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or FileNotFoundException or FormatException)
        {
            await _error.WriteLineAsync("error: " + exception.Message);
            return 2;
        }
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

    private sealed record IndustryPackCliOptions(
        string Path,
        string CoreVersion,
        string Format);
}
