namespace HeroRegistry.Domain.Common;

/// <summary>
/// Base class for persisted entities: identity, audit trail and soft delete.
/// </summary>
/// <remarks>
/// Setters are <c>private protected</c>, so only the entities in this assembly can change these values,
/// through the methods below. The Reflection data mapper (T05) can still set them when loading a row:
/// unlike <c>private</c> base-class accessors, <c>private protected</c> ones are visible through the
/// derived type's <see cref="System.Reflection.PropertyInfo"/>.
/// </remarks>
public abstract class AuditableEntity
{
    /// <summary>Database identity. 0 until the entity has been inserted.</summary>
    public int Id { get; private protected set; }

    /// <summary>Soft delete flag. Rows are never physically deleted.</summary>
    public bool IsDeleted { get; private protected set; }

    /// <summary>The user who created the entity. <see langword="null"/> for system-created rows.</summary>
    public int? CreatedBy { get; private protected set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private protected set; }

    public int? UpdatedBy { get; private protected set; }

    /// <summary>Last update or soft-delete time (UTC).</summary>
    public DateTime? UpdatedAt { get; private protected set; }

    /// <summary>Marks the entity as deleted. Deleting an already deleted entity is an error.</summary>
    public void SoftDelete(int userId, DateTime utcNow)
    {
        EnsureNotDeleted();
        MarkUpdated(userId, utcNow);
        IsDeleted = true;
    }

    private protected void MarkCreated(int? userId, DateTime utcNow)
    {
        if (userId is not null)
        {
            Guard.ValidUserId(userId.Value, nameof(CreatedBy));
        }

        Guard.Utc(utcNow, nameof(CreatedAt));

        CreatedBy = userId;
        CreatedAt = utcNow;
    }

    private protected void MarkUpdated(int userId, DateTime utcNow)
    {
        EnsureValidUpdate(userId, utcNow);

        UpdatedBy = userId;
        UpdatedAt = utcNow;
    }

    /// <summary>Checks the audit values of an update without changing anything.</summary>
    private protected void EnsureValidUpdate(int userId, DateTime utcNow)
    {
        Guard.ValidUserId(userId, nameof(UpdatedBy));
        Guard.Utc(utcNow, nameof(UpdatedAt));

        if (utcNow < CreatedAt)
        {
            throw new DomainException(nameof(UpdatedAt), "An update cannot happen before the entity was created.");
        }
    }

    private protected void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new DomainException(nameof(IsDeleted), $"This {GetType().Name} has been deleted.");
        }
    }
}
