using OpenPortalKit.Kernel.Entities;

namespace OpenPortalKit.Modules.Content.Taxonomies;

public sealed record TaxonomyTerm(
    Guid Id,
    Guid SiteId,
    TaxonomyKind Kind,
    string Code,
    string Name,
    string Slug,
    Guid? ParentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : IAuditableEntity;
