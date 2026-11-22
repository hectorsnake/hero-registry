using HeroRegistry.Application.Common;
using HeroRegistry.Domain.Users;

namespace HeroRegistry.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for user accounts, implemented in Infrastructure. Reads only return active
/// (not soft-deleted) users. Writes throw <see cref="DuplicateEntityException"/> when the user name
/// or email is already used by another active user.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The active user with this name (ignoring case and surrounding spaces), for login.</summary>
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>Every active user, in id order.</summary>
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a new user, sets its <c>Id</c> and returns it.</summary>
    Task<int> AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Persists a soft delete (call <c>user.SoftDelete</c> first). Returns false if already deleted.</summary>
    Task<bool> SoftDeleteAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Whether another active user has this name (ignoring case).</summary>
    Task<bool> ExistsByUserNameAsync(string userName, int? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Whether another active user has this email (ignoring case).</summary>
    Task<bool> ExistsByEmailAsync(string email, int? excludeId, CancellationToken cancellationToken = default);
}
