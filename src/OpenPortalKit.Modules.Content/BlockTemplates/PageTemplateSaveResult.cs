namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PageTemplateSaveResult(
    bool Succeeded,
    PageTemplate? Template,
    IReadOnlyList<string> Errors);
