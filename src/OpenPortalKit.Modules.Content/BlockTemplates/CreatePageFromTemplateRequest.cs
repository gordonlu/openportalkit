namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed record CreatePageFromTemplateRequest(
    Guid SiteId,
    string TemplateCode,
    string Title,
    string Slug,
    string Summary,
    Guid ActorId);
