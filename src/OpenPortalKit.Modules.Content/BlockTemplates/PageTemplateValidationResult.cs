namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record PageTemplateValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static PageTemplateValidationResult Success { get; } = new(Array.Empty<string>());
}
