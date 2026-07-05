namespace OpenPortalKit.Kernel.Entities;

public interface IAuditableEntity : IEntity
{
    DateTimeOffset CreatedAt { get; }

    DateTimeOffset UpdatedAt { get; }
}
