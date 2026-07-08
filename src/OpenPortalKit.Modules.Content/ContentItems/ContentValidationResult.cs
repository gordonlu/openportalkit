namespace OpenPortalKit.Modules.Content.ContentItems;

public sealed record ContentValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
