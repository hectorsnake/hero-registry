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

/// <summary>Issues access tokens (JWT in Infrastructure).</summary>
public interface ITokenService
{
    /// <summary>
    /// A token identifying the user: id (<c>sub</c>), user name (<c>unique_name</c>) and role
    /// (<see cref="Roles.Admin"/> or <see cref="Roles.User"/>).
    /// </summary>
    AccessToken CreateToken(User user);
}
