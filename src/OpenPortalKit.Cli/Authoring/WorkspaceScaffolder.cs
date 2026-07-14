using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenPortalKit.Cli.Authoring;

public sealed record WorkspaceScaffoldOptions(
    string Name,
    string Profile,
    string OutputPath,
    string SourceRoot);

public sealed record WorkspaceScaffoldResult(
    string Name,
    string Profile,
    string Path,
    string SourceChecksum,
    int FileCount);

public sealed record WorkspaceTemplateInspection(
    string TemplateVersion,
    string Checksum,
    int FileCount);

public sealed class WorkspaceScaffolder
{
    public const string ProjectSchemaVersion = "opk.project.v2";
    public const string TemplateVersion = "0.4.0-r13";

    private static readonly string[] RootFiles =
    [
        ".editorconfig", ".gitignore", "AGENTS.md", "AGENT_SEO.md", "ARCHITECTURE.md",
        "CONTRIBUTING.md", "DASHBOARD.md", "DATA_PUBLISHING.md", "DEPLOYMENT.md",
        "Directory.Build.props", "INDUSTRY_PACKS.md", "MIGRATION_FROM_LEGACY_DOTNET.md",
        "MODULES.md", "OpenPortalKit.sln", "README.md", "SECURITY.md", "global.json", "roadmap.md"
    ];

    private static readonly string[] RootDirectories =
    [
        ".github", "apps", "db", "docker", "docs", "examples", "industry-packs",
        "schemas", "src", "templates", "tests", "tools"
    ];

    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".agents", ".codex", ".next", ".vs", ".vscode", ".idea",
        "bin", "obj", "node_modules", "TestResults", "coverage", "dist", "out",
        ".appdata", ".localappdata", ".npm-cache"
    };

    public async Task<WorkspaceScaffoldResult> CreateAsync(
        WorkspaceScaffoldOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateName(options.Name);
        var sourceRoot = Path.GetFullPath(options.SourceRoot);
        ValidateSource(sourceRoot);
        var profile = await new ProjectProfileCatalog().LoadAsync(sourceRoot, options.Profile, cancellationToken);

        var outputPath = Path.GetFullPath(options.OutputPath);
        if (Directory.Exists(outputPath) || File.Exists(outputPath))
            throw new ArgumentException($"Output path already exists: {outputPath}");
        if (IsContainedBy(outputPath, sourceRoot) || IsContainedBy(sourceRoot, outputPath))
            throw new ArgumentException("The generated workspace and template source must not contain one another.");

        var parent = Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("Output path must have a parent directory.");
        Directory.CreateDirectory(parent);
        var stagingPath = Path.Combine(parent, $".{Path.GetFileName(outputPath)}.opk-tmp-{Guid.NewGuid():N}");
        var inventory = new List<TemplateFile>();

        try
        {
            Directory.CreateDirectory(stagingPath);
            foreach (var fileName in RootFiles)
            {
                var sourcePath = Path.Combine(sourceRoot, fileName);
                if (File.Exists(sourcePath))
                    await CopyFileAsync(sourceRoot, stagingPath, sourcePath, inventory, cancellationToken);
            }

            foreach (var directoryName in RootDirectories)
            {
                var sourceDirectory = Path.Combine(sourceRoot, directoryName);
                if (!Directory.Exists(sourceDirectory)) continue;
                foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                             .Order(StringComparer.Ordinal))
                {
                    var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
                    if (IsExcluded(relativePath)) continue;
                    await CopyFileAsync(sourceRoot, stagingPath, sourcePath, inventory, cancellationToken);
                }
            }

            var sourceChecksum = ComputeInventoryChecksum(inventory);
            await WriteProjectFilesAsync(
                stagingPath,
                options.Name.Trim(),
                profile,
                sourceChecksum,
                inventory.Count,
                cancellationToken);
            Directory.Move(stagingPath, outputPath);
            return new WorkspaceScaffoldResult(
                options.Name.Trim(), profile.Id, outputPath, sourceChecksum, inventory.Count);
        }
        finally
        {
            if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, recursive: true);
        }
    }

    public async Task<WorkspaceTemplateInspection> InspectSourceAsync(
        string sourceRoot,
        CancellationToken cancellationToken = default)
    {
        sourceRoot = Path.GetFullPath(sourceRoot);
        ValidateSource(sourceRoot);
        var inventory = new List<TemplateFile>();
        foreach (var sourcePath in EnumerateSourceFiles(sourceRoot))
            inventory.Add(await InspectFileAsync(sourceRoot, sourcePath, cancellationToken));
        return new WorkspaceTemplateInspection(TemplateVersion, ComputeInventoryChecksum(inventory), inventory.Count);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 100 ||
            name.Any(char.IsControl))
        {
            throw new ArgumentException("Project name must contain 2 to 100 visible characters.");
        }
    }

    private static void ValidateSource(string sourceRoot)
    {
        var requiredPaths = new[]
        {
            "OpenPortalKit.sln", "src", Path.Combine("apps", "web"), "db", "tools",
            Path.Combine("apps", "web", "src", "lib", "project-profile.json")
        };
        var missing = requiredPaths.Where(path =>
                !File.Exists(Path.Combine(sourceRoot, path)) && !Directory.Exists(Path.Combine(sourceRoot, path)))
            .ToArray();
        if (missing.Length > 0)
            throw new ArgumentException("Template source is incomplete. Missing: " + string.Join(", ", missing));
    }

    private static bool IsContainedBy(string candidate, string parent)
    {
        var normalizedParent = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate) + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedParent, PathComparison);
    }

    private static bool IsExcluded(string relativePath)
    {
        var segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(ExcludedSegments.Contains)) return true;
        var fileName = segments[^1];
        if (string.Equals(fileName, ".env.example", StringComparison.OrdinalIgnoreCase)) return false;
        return string.Equals(fileName, ".env", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".user", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".suo", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CopyFileAsync(
        string sourceRoot,
        string stagingRoot,
        string sourcePath,
        ICollection<TemplateFile> inventory,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(sourcePath);
        if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0 || fileInfo.LinkTarget is not null)
            throw new ArgumentException($"Template source contains an unsupported symbolic link: {sourcePath}");

        var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
        var destinationPath = Path.Combine(stagingRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        int read;
        long length = 0;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
            length += read;
        }

        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destinationPath, File.GetUnixFileMode(sourcePath));
        inventory.Add(new TemplateFile(NormalizePath(relativePath), length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
    }

    internal static IEnumerable<string> EnumerateSourceFiles(string sourceRoot)
    {
        foreach (var fileName in RootFiles)
        {
            var sourcePath = Path.Combine(sourceRoot, fileName);
            if (File.Exists(sourcePath)) yield return sourcePath;
        }

        foreach (var directoryName in RootDirectories)
        {
            var sourceDirectory = Path.Combine(sourceRoot, directoryName);
            if (!Directory.Exists(sourceDirectory)) continue;
            foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                if (!IsExcluded(Path.GetRelativePath(sourceRoot, sourcePath))) yield return sourcePath;
            }
        }
    }

    private static async Task<TemplateFile> InspectFileAsync(
        string sourceRoot,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(sourcePath);
        if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0 || fileInfo.LinkTarget is not null)
            throw new ArgumentException($"Template source contains an unsupported symbolic link: {sourcePath}");
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken)).ToLowerInvariant();
        return new TemplateFile(NormalizePath(Path.GetRelativePath(sourceRoot, sourcePath)), fileInfo.Length, checksum);
    }

    internal static string ComputeInventoryChecksum(IEnumerable<TemplateFile> inventory)
    {
        var payload = string.Join('\n', inventory.OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(file => $"{file.Path}\0{file.Length}\0{file.Checksum}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static async Task WriteProjectFilesAsync(
        string stagingRoot,
        string name,
        ProjectProfileDefinition profile,
        string sourceChecksum,
        int fileCount,
        CancellationToken cancellationToken)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var createdAt = DateTimeOffset.UtcNow;
        var manifest = new
        {
            schemaVersion = ProjectSchemaVersion,
            name,
            profile = new { id = profile.Id, version = profile.Version, checksum = profile.Checksum },
            createdAt,
            source = new { templateVersion = TemplateVersion, checksum = sourceChecksum, fileCount },
            selectedIndustryPacks = profile.SelectedIndustryPacks
        };
        await File.WriteAllTextAsync(Path.Combine(stagingRoot, "openportalkit.project.json"),
            JsonSerializer.Serialize(manifest, jsonOptions) + Environment.NewLine, cancellationToken);

        var profilePath = Path.Combine(stagingRoot, "apps", "web", "src", "lib", "project-profile.json");
        var profileDocument = new
        {
            schemaVersion = "opk.project-profile.v2",
            projectName = name,
            defaultSite = profile.DefaultSite,
            profileTemplate = new { id = profile.Id, version = profile.Version, checksum = profile.Checksum },
            selectedIndustryPacks = profile.SelectedIndustryPacks
        };
        await File.WriteAllTextAsync(profilePath,
            JsonSerializer.Serialize(profileDocument, jsonOptions) + Environment.NewLine, cancellationToken);

        var projectReadme = $"""
            # {name}

            This source workspace was generated from OpenPortalKit template {TemplateVersion} with the `{profile.Id}` profile version {profile.Version}.
            Framework modules remain under `src/`; site-specific work should stay in the Web app, configuration, or a matching industry pack.

            ## First validation

            ```powershell
            dotnet build OpenPortalKit.sln -m:1
            powershell -ExecutionPolicy Bypass -File ./tools/check-boundaries.ps1
            ```

            The selected industry packs are recorded in `openportalkit.project.json`; selecting a pack does not silently enable it in a database.
            """;
        await File.WriteAllTextAsync(Path.Combine(stagingRoot, "PROJECT.md"),
            projectReadme + Environment.NewLine, cancellationToken);
    }

    private static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal sealed record TemplateFile(string Path, long Length, string Checksum);
}
