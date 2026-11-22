using HeroRegistry.Domain.Users;

namespace HeroRegistry.Application.Abstractions.Security;

/// <summary>Role names carried in the access token's <c>role</c> claim and checked by the API.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

/// <summary>One-way password hashing (implemented with BCrypt in Infrastructure).</summary>
public interface IPasswordHasher
{
    /// <summary>A salted hash to store instead of the password.</summary>
    string Hash(string password);

    /// <summary>
    /// Whether <paramref name="password"/> matches <paramref name="hash"/>. With a <see langword="null"/>
    /// hash (unknown user) it still performs a full verification against a dummy hash and returns
    /// <see langword="false"/>, so "unknown user" and "wrong password" take the same time.
    /// Never throws for a malformed hash.
    /// </summary>
    bool Verify(string password, string? hash);
}

/// <summary>A signed access token and its expiration (UTC).</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>A new refresh token: the raw value (for the cookie only) and its hash (for the database only).</summary>
public sealed record NewRefreshToken(string Token, string Hash)
{
    /// <summary>Never prints the raw token, so logging this record cannot leak it.</summary>
    public override string ToString() => $"NewRefreshToken {{ Hash = {Hash} }}";
}

/// <summary>Issues access tokens (JWT in Infrastructure) and refresh tokens.</summary>
public interface ITokenService
{
    /// <summary>
    /// A token identifying the user: id (<c>sub</c>), user name (<c>unique_name</c>) and role
    /// (<see cref="Roles.Admin"/> or <see cref="Roles.User"/>).
    /// </summary>
    AccessToken CreateToken(User user);

    /// <summary>An opaque, cryptographically random refresh token (not a JWT) and its SHA-256 hash.</summary>
    NewRefreshToken CreateRefreshToken();

    /// <summary>The SHA-256 hash (lower-case hex) under which a refresh token is stored.</summary>
    string HashRefreshToken(string token);
}
