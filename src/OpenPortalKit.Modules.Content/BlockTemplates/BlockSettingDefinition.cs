namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record BlockSettingDefinition(
    string Key,
    string Label,
    BlockSettingType Type,
    bool IsRequired,
    string? Description = null);
