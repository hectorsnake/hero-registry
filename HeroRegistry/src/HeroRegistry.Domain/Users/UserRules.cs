namespace HeroRegistry.Domain.Users;

/// <summary>User account rules, shared by the entity and the Application validators (T08).</summary>
public static class UserRules
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 50;

    /// <summary>Letters, digits, dot, underscore and hyphen (ASCII only).</summary>
    public const string UserNamePattern = "^[a-zA-Z0-9._-]+$";

    /// <summary>Size of the Password column, which stores a hash (a BCrypt hash is 60 characters).</summary>
    public const int PasswordHashMaxLength = 200;

    /// <summary>Password policy for new accounts: minimum length (plus upper, lower, digit and symbol).</summary>
    public const int PasswordMinLength = 8;

    public const int PasswordMaxLength = 64;

    /// <summary>BCrypt only uses the first 72 bytes of a password, so longer ones are rejected rather than silently cut.</summary>
    public const int PasswordMaxBytes = 72;
}
