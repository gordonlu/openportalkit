namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public sealed record AgentOutputArtifact(
    string Path,
    string ContentType,
    string Body,
    string SourceId,
    string SourceKind,
    string SchemaVersion,
    string Checksum,
    DateTimeOffset GeneratedAt);
