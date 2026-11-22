using HeroRegistry.Application.Common;
using HeroRegistry.Application.Heroes;
using HeroRegistry.Domain.Heroes;

namespace HeroRegistry.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for superheroes, implemented in Infrastructure. Reads only return active
/// (not soft-deleted) heroes. Writes throw <see cref="DuplicateEntityException"/> when the hero name
/// or email is already used by another active hero.
/// </summary>
public interface IHeroRepository
{
    Task<SuperHero?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Searches, sorts and pages active heroes. The query is normalized first.</summary>
    Task<PagedResult<SuperHero>> SearchAsync(HeroQuery query, CancellationToken cancellationToken = default);

    /// <summary>Stores a new hero, sets its <c>Id</c> and returns it.</summary>
    Task<int> AddAsync(SuperHero hero, CancellationToken cancellationToken = default);

    /// <summary>Saves the hero's changes. Returns false if it no longer exists or was deleted.</summary>
    Task<bool> UpdateAsync(SuperHero hero, CancellationToken cancellationToken = default);

    /// <summary>Persists a soft delete (call <c>hero.SoftDelete</c> first). Returns false if already deleted.</summary>
    Task<bool> SoftDeleteAsync(SuperHero hero, CancellationToken cancellationToken = default);

    /// <summary>Whether another active hero uses this name (ignoring case).</summary>
    /// <param name="excludeId">The hero being updated, so it can keep its own name.</param>
    Task<bool> ExistsByHeroNameAsync(string heroName, int? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Whether another active hero uses this email (ignoring case).</summary>
    Task<bool> ExistsByEmailAsync(string email, int? excludeId, CancellationToken cancellationToken = default);
}
