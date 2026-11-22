using HeroRegistry.Domain.Common;

namespace HeroRegistry.Infrastructure.Persistence.Mapping;

/// <summary>
/// Conventions shared by every <see cref="AuditableEntity"/>: identity key, creation audit that is never
/// updated, and soft delete that also records who deleted the row and when.
/// </summary>
public abstract class AuditableEntityMap<T> : EntityMap<T>
    where T : AuditableEntity
{
    protected AuditableEntityMap()
    {
        HasKey(e => e.Id);
        Property(e => e.CreatedBy).IsInsertOnly();
        Property(e => e.CreatedAt).IsInsertOnly();
        HasSoftDelete(e => e.IsDeleted, e => e.UpdatedBy, e => e.UpdatedAt);
    }
}
