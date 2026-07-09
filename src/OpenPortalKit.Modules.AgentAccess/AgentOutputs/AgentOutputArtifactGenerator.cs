using System.Text;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public static class AgentOutputArtifactGenerator
{
    public const string SchemaVersion = "agent-output.v1";

    public static IReadOnlyList<AgentOutputArtifact> GenerateContentArtifacts(
        AgentContentDocument document,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(document);

        var markdownPath = "/content/" + document.Slug + ".md";
        var jsonPath = "/api/public/content/" + document.Slug + ".json";

        return new[]
        {
            CreateArtifact(
                markdownPath,
                "text/markdown; charset=utf-8",
                AgentSnapshotGenerator.GenerateMarkdown(document),
                document.Id,
                "ContentItem",
                generatedAt),
            CreateArtifact(
                jsonPath,
                "application/json; charset=utf-8",
                AgentSnapshotGenerator.GenerateJson(document),
                document.Id,
                "ContentItem",
                generatedAt)
        };
    }

    public static AgentOutputArtifact CreateArtifact(
        string path,
        string contentType,
        string body,
        string sourceId,
        string sourceKind,
        DateTimeOffset generatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKind);

        return new AgentOutputArtifact(
            path,
            contentType,
            body,
            sourceId,
            sourceKind,
            SchemaVersion,
            ComputeChecksum(body),
            generatedAt);
    }

    public static string ComputeChecksum(string text)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        foreach (var valueByte in Encoding.UTF8.GetBytes(text))
        {
            hash ^= valueByte;
            hash *= prime;
        }

        return hash.ToString("x16");
    }
}
