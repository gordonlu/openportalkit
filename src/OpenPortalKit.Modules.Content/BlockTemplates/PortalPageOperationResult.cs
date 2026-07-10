namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PortalPageOperationResult(
    bool Succeeded,
    PortalPage? Page,
    IReadOnlyList<string> Errors);
