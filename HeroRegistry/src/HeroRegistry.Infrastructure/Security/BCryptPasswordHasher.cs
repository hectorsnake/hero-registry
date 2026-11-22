using HeroRegistry.Application.Abstractions.Security;

namespace HeroRegistry.Infrastructure.Security;

/// <summary>
/// BCrypt password hashing (BCrypt.Net-Next): salted, deliberately slow, and compatible with the
/// <c>$2a$11$</c> hash of the seeded administrator (0003_SeedData.sql).
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>2^11 rounds: roughly 100 ms per hash on current hardware, too slow for brute force.</summary>
    public const int WorkFactor = 11;

    /// <summary>
    /// Verified against when the user does not exist, so that path costs the same as a real check.
    /// Created once, from a random value nobody knows.
    /// </summary>
    private static readonly Lazy<string> DummyHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), WorkFactor));

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string? hash)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (string.IsNullOrEmpty(hash))
        {
            BCrypt.Net.BCrypt.Verify(password, DummyHash.Value);
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
        {
            // A corrupt stored hash must fail the login, not crash it.
            return false;
        }
    }
}
