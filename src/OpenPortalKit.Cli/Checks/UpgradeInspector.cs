using System.Text.Json;
using OpenPortalKit.Cli.Authoring;

namespace OpenPortalKit.Cli.Checks;

public sealed class UpgradeInspector
{
    public async Task<CheckReport> InspectAsync(
        string workspaceRoot,
        string candidateSourceRoot,
        CancellationToken cancellationToken = default)
    {
        workspaceRoot = Path.GetFullPath(workspaceRoot);
        candidateSourceRoot = Path.GetFullPath(candidateSourceRoot);
        var results = new List<CheckResult>();
        var manifestPath = Path.Combine(workspaceRoot, "openportalkit.project.json");
        if (!File.Exists(manifestPath))
        {
            results.Add(Fail("OPK-UPG-001", "project manifest", "openportalkit.project.json was not found."));
            return new CheckReport("OpenPortalKit upgrade inspection", results);
        }

        ProjectProvenance provenance;
        try
        {
            provenance = await ReadProvenanceAsync(manifestPath, cancellationToken);
            results.Add(Pass("OPK-UPG-001", "project manifest", $"Manifest {WorkspaceScaffolder.ProjectSchemaVersion} contains complete source and profile provenance."));
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            results.Add(Fail("OPK-UPG-001", "project manifest", exception.Message));
            return new CheckReport("OpenPortalKit upgrade inspection", results);
        }

        var scaffolder = new WorkspaceScaffolder();
        var candidate = await scaffolder.InspectSourceAsync(candidateSourceRoot, cancellationToken);
        results.Add(provenance.SourceTemplateVersion == candidate.TemplateVersion
            ? Pass("OPK-UPG-002", "source template version", $"Workspace and candidate use {candidate.TemplateVersion}.")
            : Warn("OPK-UPG-002", "source template version", $"Workspace uses {provenance.SourceTemplateVersion}; candidate is {candidate.TemplateVersion}."));
        results.Add(provenance.SourceChecksum == candidate.Checksum
            ? Pass("OPK-UPG-003", "source template checksum", $"Candidate matches the recorded {candidate.FileCount}-file source inventory.")
            : Warn("OPK-UPG-003", "source template checksum", $"Candidate source differs from recorded provenance ({provenance.SourceFileCount} recorded files; {candidate.FileCount} candidate files). Review release changes before upgrading."));

        var candidateProfile = await new ProjectProfileCatalog()
            .LoadAsync(candidateSourceRoot, provenance.ProfileId, cancellationToken);
        results.Add(provenance.ProfileChecksum == candidateProfile.Checksum
            ? Pass("OPK-UPG-004", "project profile", $"Profile {candidateProfile.Id} {candidateProfile.Version} is unchanged.")
            : Warn("OPK-UPG-004", "project profile", $"Profile {provenance.ProfileId} changed from recorded version {provenance.ProfileVersion} to candidate version {candidateProfile.Version}."));

        var boundaryReport = new BoundaryChecker().Run(workspaceRoot);
        var boundaryFailures = boundaryReport.Results.Where(result => result.Status == CheckStatus.Failed).ToArray();
        results.Add(boundaryFailures.Length == 0
            ? Pass("OPK-UPG-005", "workspace boundaries", "Current customer code passes all repository boundary checks.")
            : Fail("OPK-UPG-005", "workspace boundaries", string.Join("; ", boundaryFailures.Select(result => result.Message))));

        return new CheckReport("OpenPortalKit upgrade inspection", results);
    }

    private static async Task<ProjectProvenance> ReadProvenanceAsync(string path, CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > 1024 * 1024)
            throw new FormatException("Project manifest exceeds the 1 MiB limit.");
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 16 }, cancellationToken);
        var root = document.RootElement;
        if (RequiredString(root, "schemaVersion") != WorkspaceScaffolder.ProjectSchemaVersion)
            throw new FormatException($"Upgrade inspection requires {WorkspaceScaffolder.ProjectSchemaVersion} provenance.");
        var profile = RequiredObject(root, "profile");
        var source = RequiredObject(root, "source");
        var sourceChecksum = RequiredChecksum(source, "checksum");
        var profileChecksum = RequiredChecksum(profile, "checksum");
        if (!source.TryGetProperty("fileCount", out var count) || !count.TryGetInt32(out var fileCount) || fileCount < 1)
            throw new FormatException("Project manifest source.fileCount must be a positive integer.");
        return new ProjectProvenance(
            RequiredString(source, "templateVersion"), sourceChecksum, fileCount,
            RequiredString(profile, "id"), RequiredString(profile, "version"), profileChecksum);
    }

    private static JsonElement RequiredObject(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new FormatException($"Project manifest property '{name}' must be an object.");
        return value;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new FormatException($"Project manifest property '{name}' must be a non-empty string.");
        return value.GetString()!;
    }

    private static string RequiredChecksum(JsonElement element, string name)
    {
        var value = RequiredString(element, name);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new FormatException($"Project manifest property '{name}' must be a SHA-256 checksum.");
        return value.ToLowerInvariant();
    }

    private static CheckResult Pass(string code, string target, string message) =>
        new(code, CheckStatus.Passed, target, message);

    private static CheckResult Warn(string code, string target, string message) =>
        new(code, CheckStatus.Warning, target, message);

    private static CheckResult Fail(string code, string target, string message) =>
        new(code, CheckStatus.Failed, target, message);

    private sealed record ProjectProvenance(
        string SourceTemplateVersion,
        string SourceChecksum,
        int SourceFileCount,
        string ProfileId,
        string ProfileVersion,
        string ProfileChecksum);
}
