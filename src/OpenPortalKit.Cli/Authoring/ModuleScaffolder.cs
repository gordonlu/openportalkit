using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenPortalKit.Cli.Checks;

namespace OpenPortalKit.Cli.Authoring;

public sealed record ModuleScaffoldOptions(
    string Name,
    string Area,
    string Description,
    bool OwnsBusinessState,
    IReadOnlyList<string> PublicOutputs,
    string RepositoryRoot);

public sealed record ModuleScaffoldResult(
    string Name,
    string SourcePath,
    string TestPath,
    IReadOnlyList<string> PublicOutputs);

public sealed partial class ModuleScaffolder
{
    private static readonly IReadOnlyDictionary<string, string> SupportedPublicOutputs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HTML"] = "HTML",
            ["Markdown"] = "Markdown",
            ["JSON"] = "JSON",
            ["Sitemap"] = "Sitemap",
            ["RSS"] = "RSS",
            ["Search"] = "Search",
            ["AgentSEO"] = "AgentSEO"
        };

    public async Task<ModuleScaffoldResult> CreateAsync(
        ModuleScaffoldOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateText(options);

        var root = Path.GetFullPath(options.RepositoryRoot);
        var solutionPath = Path.Combine(root, "OpenPortalKit.sln");
        var kernelProject = Path.Combine(root, "src", "OpenPortalKit.Kernel", "OpenPortalKit.Kernel.csproj");
        if (!File.Exists(solutionPath) || !File.Exists(kernelProject))
            throw new DirectoryNotFoundException("The repository must contain OpenPortalKit.sln and the Kernel project.");

        var initialBoundaryReport = new BoundaryChecker().Run(root);
        if (!initialBoundaryReport.IsSuccessful)
            throw new ArgumentException("Repository boundary checks must pass before adding a module.");

        var projectName = "OpenPortalKit.Modules." + options.Name;
        var testProjectName = projectName + ".Tests";
        var sourcePath = Path.Combine(root, "src", projectName);
        var testPath = Path.Combine(root, "tests", testProjectName);
        EnsureDestinationIsAvailable(sourcePath);
        EnsureDestinationIsAvailable(testPath);

        var outputs = NormalizePublicOutputs(options.PublicOutputs);
        var operationId = Guid.NewGuid().ToString("N");
        var sourceStage = Path.Combine(root, "src", ".opk-module-" + operationId);
        var testStage = Path.Combine(root, "tests", ".opk-module-tests-" + operationId);
        var solutionBackup = await File.ReadAllBytesAsync(solutionPath, cancellationToken);
        var sourceMoved = false;
        var testMoved = false;

        try
        {
            await WriteSourceProjectAsync(sourceStage, projectName, options, outputs, cancellationToken);
            await WriteTestProjectAsync(testStage, projectName, testProjectName, options, outputs, cancellationToken);
            Directory.Move(sourceStage, sourcePath);
            sourceMoved = true;
            Directory.Move(testStage, testPath);
            testMoved = true;

            await AddToSolutionAsync(solutionPath, Path.Combine(sourcePath, projectName + ".csproj"), "src", root, cancellationToken);
            await AddToSolutionAsync(solutionPath, Path.Combine(testPath, testProjectName + ".csproj"), "tests", root, cancellationToken);

            var boundaryReport = new BoundaryChecker().Run(root);
            if (!boundaryReport.IsSuccessful)
            {
                var failures = boundaryReport.Results
                    .Where(result => result.Status == CheckStatus.Failed)
                    .Select(result => result.Message);
                throw new ArgumentException("Generated module violates repository boundaries: " + string.Join("; ", failures));
            }

            return new ModuleScaffoldResult(options.Name, sourcePath, testPath, outputs);
        }
        catch
        {
            await File.WriteAllBytesAsync(solutionPath, solutionBackup, CancellationToken.None);
            DeleteDirectory(sourceStage);
            DeleteDirectory(testStage);
            if (sourceMoved) DeleteDirectory(sourcePath);
            if (testMoved) DeleteDirectory(testPath);
            throw;
        }
    }

    private static void ValidateText(ModuleScaffoldOptions options)
    {
        if (!ModuleNamePattern().IsMatch(options.Name) || options.Name.Length > 64)
            throw new ArgumentException("Module name must start with an uppercase letter, contain only ASCII letters and digits, and be at most 64 characters.");
        if (!AreaPattern().IsMatch(options.Area) || options.Area.Length > 48)
            throw new ArgumentException("Module area must be a lowercase ASCII slug of at most 48 characters.");
        if (string.IsNullOrWhiteSpace(options.Description) || options.Description.Length > 240 ||
            options.Description.IndexOfAny(['\r', '\n']) >= 0)
            throw new ArgumentException("Module description must be one line containing 1 to 240 characters.");
    }

    private static IReadOnlyList<string> NormalizePublicOutputs(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!SupportedPublicOutputs.TryGetValue(value.Trim(), out var canonical))
                throw new ArgumentException($"Unsupported public output '{value}'. Supported values: {string.Join(", ", SupportedPublicOutputs.Values)}.");
            normalized.Add(canonical);
        }
        return normalized.ToArray();
    }

    private static async Task WriteSourceProjectAsync(
        string path,
        string projectName,
        ModuleScaffoldOptions options,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, projectName + ".csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <ItemGroup>
                <ProjectReference Include="..\OpenPortalKit.Kernel\OpenPortalKit.Kernel.csproj" />
              </ItemGroup>

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

            </Project>
            """ + Environment.NewLine, cancellationToken);

        var outputExpression = outputs.Count == 0
            ? "Array.Empty<string>()"
            : "new[] { " + string.Join(", ", outputs.Select(value => JsonSerializer.Serialize(value))) + " }";
        await File.WriteAllTextAsync(Path.Combine(path, options.Name + "Module.cs"), $$"""
            using OpenPortalKit.Kernel.Modules;

            namespace {{projectName}};

            public static class {{options.Name}}Module
            {
                public static OpenPortalKitModuleDescriptor Descriptor { get; } = new(
                    {{JsonSerializer.Serialize(options.Name)}},
                    {{JsonSerializer.Serialize(options.Area)}},
                    {{JsonSerializer.Serialize(options.Description.Trim())}},
                    {{options.OwnsBusinessState.ToString().ToLowerInvariant()}},
                    {{outputExpression}});
            }
            """ + Environment.NewLine, cancellationToken);

        await File.WriteAllTextAsync(Path.Combine(path, "README.md"), $$"""
            # {{options.Name}} Module

            Area: `{{options.Area}}`

            {{options.Description.Trim()}}

            ## Ownership

            - Owns business state: `{{options.OwnsBusinessState.ToString().ToLowerInvariant()}}`
            - Public outputs: {{(outputs.Count == 0 ? "none" : string.Join(", ", outputs.Select(value => "`" + value + "`")))}}
            - Dependencies: `OpenPortalKit.Kernel` only

            ## Integration Decisions

            Before adding behavior, document owned data, contracts, audit events, dashboard impact,
            AgentSEO representations, and public read authorization. Public-output-changing actions
            must emit an audit record. Industry-specific concepts belong in `industry-packs/`.
            """ + Environment.NewLine, cancellationToken);
    }

    private static async Task WriteTestProjectAsync(
        string path,
        string projectName,
        string testProjectName,
        ModuleScaffoldOptions options,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, testProjectName + ".csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <ItemGroup>
                <ProjectReference Include="..\..\src\{{projectName}}\{{projectName}}.csproj" />
              </ItemGroup>

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

            </Project>
            """ + Environment.NewLine, cancellationToken);

        var expectedOutputs = outputs.Count == 0
            ? "Array.Empty<string>()"
            : "new[] { " + string.Join(", ", outputs.Select(value => JsonSerializer.Serialize(value))) + " }";
        await File.WriteAllTextAsync(Path.Combine(path, "Program.cs"), $$"""
            using {{projectName}};

            var descriptor = {{options.Name}}Module.Descriptor;

            Assert(descriptor.Name == {{JsonSerializer.Serialize(options.Name)}}, "Module name changed unexpectedly.");
            Assert(descriptor.Area == {{JsonSerializer.Serialize(options.Area)}}, "Module area changed unexpectedly.");
            Assert(descriptor.OwnsBusinessState == {{options.OwnsBusinessState.ToString().ToLowerInvariant()}}, "State ownership changed unexpectedly.");
            Assert(descriptor.PublicOutputs.SequenceEqual({{expectedOutputs}}), "Public output contract changed unexpectedly.");

            Console.WriteLine("PASS {{options.Name}} module descriptor contract");
            return 0;

            static void Assert(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }
            """ + Environment.NewLine, cancellationToken);
    }

    private static async Task AddToSolutionAsync(
        string solutionPath,
        string projectPath,
        string solutionFolder,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("solution");
        process.StartInfo.ArgumentList.Add(solutionPath);
        process.StartInfo.ArgumentList.Add("add");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("--solution-folder");
        process.StartInfo.ArgumentList.Add(solutionFolder);

        if (!process.Start()) throw new IOException("Failed to start the dotnet solution command.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
            throw new IOException($"dotnet solution add failed: {(string.IsNullOrWhiteSpace(error) ? output : error).Trim()}");
    }

    private static void EnsureDestinationIsAvailable(string path)
    {
        if (Directory.Exists(path) || File.Exists(path))
            throw new ArgumentException($"Module destination already exists: {path}");
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleNamePattern();

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex AreaPattern();
}
