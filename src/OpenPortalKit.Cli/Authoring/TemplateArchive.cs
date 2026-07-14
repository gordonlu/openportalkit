using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenPortalKit.Cli.Authoring;

public sealed record TemplateArchiveResult(string Path, string SourceChecksum, string ArchiveChecksum, int FileCount);

public sealed class TemplateArchiveExtraction : IAsyncDisposable
{
    internal TemplateArchiveExtraction(string path) => Path = path;
    public string Path { get; }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        return ValueTask.CompletedTask;
    }
}

public sealed class TemplateArchive
{
    public const string SchemaVersion = "opk.source-template-archive.v1";
    private const string ManifestName = "opk-template.json";
    private const int MaximumFiles = 10_000;
    private const long MaximumFileBytes = 128L * 1024 * 1024;
    private const long MaximumTotalBytes = 2L * 1024 * 1024 * 1024;

    public async Task<TemplateArchiveResult> PackAsync(
        string sourceRoot,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        sourceRoot = Path.GetFullPath(sourceRoot);
        outputPath = Path.GetFullPath(outputPath);
        if (File.Exists(outputPath) || Directory.Exists(outputPath))
            throw new ArgumentException($"Archive output already exists: {outputPath}");

        var inspection = await new WorkspaceScaffolder().InspectSourceAsync(sourceRoot, cancellationToken);
        if (inspection.FileCount > MaximumFiles)
            throw new ArgumentException($"Template contains more than {MaximumFiles} files.");
        var parent = Path.GetDirectoryName(outputPath) ?? throw new ArgumentException("Archive output must have a parent directory.");
        Directory.CreateDirectory(parent);
        var stagingPath = Path.Combine(parent, $".{Path.GetFileName(outputPath)}.tmp-{Guid.NewGuid():N}");
        try
        {
            await using (var output = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var sourcePath in WorkspaceScaffolder.EnumerateSourceFiles(sourceRoot))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var info = new FileInfo(sourcePath);
                    if (info.Length > MaximumFileBytes)
                        throw new ArgumentException($"Template file exceeds the {MaximumFileBytes}-byte limit: {sourcePath}");
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
                        throw new ArgumentException($"Template source contains an unsupported symbolic link: {sourcePath}");
                    var relativePath = Path.GetRelativePath(sourceRoot, sourcePath).Replace(Path.DirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                    if (!OperatingSystem.IsWindows()) entry.ExternalAttributes = ((int)File.GetUnixFileMode(sourcePath) & 0xFFF) << 16;
                    await using var entryStream = entry.Open();
                    await using var source = File.OpenRead(sourcePath);
                    await source.CopyToAsync(entryStream, cancellationToken);
                }

                var manifestEntry = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
                await using var manifestStream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(manifestStream, new
                {
                    schemaVersion = SchemaVersion,
                    templateVersion = inspection.TemplateVersion,
                    createdAt = DateTimeOffset.UtcNow,
                    sourceChecksum = inspection.Checksum,
                    fileCount = inspection.FileCount
                }, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
            }

            File.Move(stagingPath, outputPath);
            await using var archiveStream = File.OpenRead(outputPath);
            var archiveChecksum = Convert.ToHexString(await SHA256.HashDataAsync(archiveStream, cancellationToken)).ToLowerInvariant();
            return new TemplateArchiveResult(outputPath, inspection.Checksum, archiveChecksum, inspection.FileCount);
        }
        finally
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
        }
    }

    public async Task<TemplateArchiveExtraction> ExtractAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        archivePath = Path.GetFullPath(archivePath);
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Template archive was not found.", archivePath);
        var destination = Path.Combine(Path.GetTempPath(), "opk-template-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var manifests = archive.Entries.Where(entry => entry.FullName == ManifestName).ToArray();
            if (manifests.Length != 1 || manifests[0].Length > 1024 * 1024)
                throw new FormatException("Template archive must contain one bounded opk-template.json manifest.");
            ArchiveManifest manifest;
            await using (var manifestStream = manifests[0].Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<ArchiveManifest>(manifestStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
                    ?? throw new FormatException("Template archive manifest is empty.");
            }
            if (manifest.SchemaVersion != SchemaVersion || manifest.TemplateVersion != WorkspaceScaffolder.TemplateVersion ||
                manifest.SourceChecksum is null || manifest.SourceChecksum.Length != 64 ||
                manifest.FileCount is < 1 or > MaximumFiles)
                throw new FormatException("Template archive manifest is unsupported or incomplete.");

            var entries = archive.Entries.Where(entry => entry.FullName != ManifestName).ToArray();
            if (entries.Length != manifest.FileCount)
                throw new FormatException("Template archive file count does not match its manifest.");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            long totalBytes = 0;
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = ValidateEntryPath(entry.FullName);
                if (!paths.Add(relativePath)) throw new FormatException($"Duplicate archive path: {relativePath}");
                if (entry.Length > MaximumFileBytes || (totalBytes += entry.Length) > MaximumTotalBytes)
                    throw new FormatException("Template archive exceeds extraction limits.");
                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if (unixType == 0xA000) throw new FormatException($"Symbolic links are not supported: {relativePath}");
                var targetPath = Path.Combine(destination, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await using var source = entry.Open();
                await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(target, cancellationToken);
                if (!OperatingSystem.IsWindows() && entry.ExternalAttributes != 0)
                    File.SetUnixFileMode(targetPath, (UnixFileMode)((entry.ExternalAttributes >> 16) & 0xFFF));
            }

            var inspection = await new WorkspaceScaffolder().InspectSourceAsync(destination, cancellationToken);
            if (inspection.FileCount != manifest.FileCount || inspection.Checksum != manifest.SourceChecksum)
                throw new FormatException("Template archive source checksum verification failed.");
            return new TemplateArchiveExtraction(destination);
        }
        catch
        {
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            throw;
        }
    }

    private static string ValidateEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith('/') || path.EndsWith('/') ||
            path.Split('/').Any(segment => segment is "" or "." or "..") || path.Contains(':'))
            throw new FormatException($"Unsafe template archive path: {path}");
        return path;
    }

    private sealed record ArchiveManifest(
        string? SchemaVersion,
        string? TemplateVersion,
        DateTimeOffset CreatedAt,
        string? SourceChecksum,
        int FileCount);
}
