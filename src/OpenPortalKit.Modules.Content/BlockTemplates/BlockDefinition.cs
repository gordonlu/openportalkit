namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record BlockDefinition(
    string Code,
    string DisplayName,
    string Description,
    string SchemaVersion,
    IReadOnlyList<BlockSettingDefinition> Settings);
