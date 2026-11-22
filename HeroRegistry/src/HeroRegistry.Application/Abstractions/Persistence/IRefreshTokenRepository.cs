using HeroRegistry.Domain.Users;

namespace HeroRegistry.Application.Abstractions.Persistence;

/// <summary>Persistence port for refresh tokens (T09.1), implemented in Infrastructure.</summary>
public interface IRefreshTokenRepository
{
    /// <summary>The token with this hash, active or not, or <see langword="null"/>.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Stores a new token, sets its <c>Id</c> and returns it.</summary>
    Task<int> AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists <c>token.Revoke(...)</c>, but only if the stored token is still active (atomic). Returns
    /// <see langword="false"/> when another request revoked or rotated it first.
    /// </summary>
    Task<bool> RevokeAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>Revokes every still-active token of a family (theft detected, or logout). Returns the count.</summary>
    Task<int> RevokeFamilyAsync(Guid familyId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Revokes every still-active token of a user (the user was deleted). Returns the count.</summary>
    Task<int> RevokeAllForUserAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Physically deletes a user's expired tokens (housekeeping). Returns the count.</summary>
    Task<int> DeleteExpiredAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default);
}
