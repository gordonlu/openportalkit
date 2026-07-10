namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record BlockInstance(
    Guid Id,
    string DefinitionCode,
    string SchemaVersion,
    int SortOrder,
    string ConfigurationJson);
