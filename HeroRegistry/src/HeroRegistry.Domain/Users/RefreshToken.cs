using System.Text.RegularExpressions;
using HeroRegistry.Domain.Common;

namespace HeroRegistry.Domain.Users;

/// <summary>
/// A long-lived refresh token (T09.1, AC12). Only its SHA-256 hash is stored; the raw value lives in
/// the user's HttpOnly cookie. Tokens are rotated on every use: the used token is revoked and replaced
/// by a new one in the same <see cref="FamilyId"/> (one family per login), which makes reuse of an old
/// token detectable.
/// </summary>
public sealed class RefreshToken
{
    private static readonly Regex Sha256Hex = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Used by the Reflection data mapper (T05) to load a row. Application code uses Issue.
    private RefreshToken()
    {
    }

    public int Id { get; private set; }

    public int UserId { get; private set; }

    /// <summary>Lower-case hex SHA-256 of the raw token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Every token issued from one login, through all its rotations.</summary>
    public Guid FamilyId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    /// <summary>When the token was rotated, revoked by logout, or revoked with its family.</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>The hash of the token that replaced this one; null unless it was rotated.</summary>
    public string? ReplacedByHash { get; private set; }

    /// <exception cref="DomainException">An argument is invalid.</exception>
    public static RefreshToken Issue(int userId, string tokenHash, Guid familyId, DateTime utcNow, TimeSpan lifetime)
    {
        Guard.ValidUserId(userId, nameof(UserId));
        Guard.Utc(utcNow, nameof(CreatedAt));
        EnsureHash(tokenHash, nameof(TokenHash));

        if (familyId == Guid.Empty)
        {
            throw new DomainException(nameof(FamilyId), "FamilyId is required.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException(nameof(ExpiresAt), "A refresh token must have a positive lifetime.");
        }

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAt = utcNow,
            ExpiresAt = utcNow + lifetime,
        };
    }

    /// <summary>Usable to get a new access token: not revoked and not expired.</summary>
    public bool IsActive(DateTime utcNow) => RevokedAt is null && utcNow < ExpiresAt;

    /// <summary>
    /// Revokes the token. With <paramref name="replacedByHash"/> it is a rotation (the normal refresh);
    /// without it, a plain revocation (logout).
    /// </summary>
    /// <exception cref="DomainException">The token was already revoked.</exception>
    public void Revoke(DateTime utcNow, string? replacedByHash = null)
    {
        Guard.Utc(utcNow, nameof(RevokedAt));

        if (RevokedAt is not null)
        {
            throw new DomainException(nameof(RevokedAt), "This refresh token has already been revoked.");
        }

        if (replacedByHash is not null)
        {
            EnsureHash(replacedByHash, nameof(ReplacedByHash));
        }

        RevokedAt = utcNow;
        ReplacedByHash = replacedByHash;
    }

    /// <summary>
    /// Whether the token was rotated at most <paramref name="window"/> ago. Two tabs refreshing at the
    /// same moment both send the old cookie; within this window that is a race, not a theft.
    /// </summary>
    public bool WasRotatedWithin(DateTime utcNow, TimeSpan window) =>
        RevokedAt is { } revokedAt && ReplacedByHash is not null && utcNow - revokedAt <= window;

    private static void EnsureHash(string? hash, string field)
    {
        if (hash is null || !Sha256Hex.IsMatch(hash))
        {
            throw new DomainException(field, $"{field} must be a lower-case hexadecimal SHA-256 hash.");
        }
    }
}
