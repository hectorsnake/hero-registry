using System.Text.RegularExpressions;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.ValueObjects;
using static HeroRegistry.Domain.Users.UserRules;

namespace HeroRegistry.Domain.Users;

/// <summary>An account that can log in to the registry.</summary>
public sealed class User : AuditableEntity
{
    private static readonly Regex UserNameFormat = new(
        UserNamePattern,
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    // Used by the Reflection data mapper (T05) to load a row. Application code uses Register.
    private User()
    {
    }

    /// <summary>Unique among active users, ignoring case.</summary>
    public string UserName { get; private set; } = string.Empty;

    /// <summary>Trimmed and lower-cased. Unique among active users.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// The password <b>hash</b> (BCrypt). The domain never sees a plain-text password: hashing and the
    /// password policy belong to the Application and Infrastructure layers (T08).
    /// </summary>
    public string Password { get; private set; } = string.Empty;

    public bool IsAdmin { get; private set; }

    /// <summary>
    /// Creates a regular (non-admin) account. Self-registration can never create an administrator.
    /// </summary>
    /// <param name="createdBy">The user creating the account, or <see langword="null"/> for self-registration.</param>
    /// <exception cref="DomainException">A rule is violated.</exception>
    public static User Register(string userName, string email, string passwordHash, DateTime utcNow, int? createdBy = null)
    {
        var user = new User
        {
            UserName = ValidateUserName(userName),
            Email = EmailAddress.Create(email, nameof(Email)).Value,
            Password = Guard.RequiredText(passwordHash, nameof(Password), 1, PasswordHashMaxLength),
            IsAdmin = false,
        };

        user.MarkCreated(createdBy, utcNow);
        return user;
    }

    /// <summary>
    /// Changes the user name and email (AC11). Both are validated and normalized like in
    /// <see cref="Register"/> before anything changes. The password, the administrator flag and the
    /// creation audit are never touched.
    /// </summary>
    /// <exception cref="DomainException">A rule is violated, or the account has been deleted.</exception>
    public void UpdateAccount(string userName, string email, int updatedBy, DateTime utcNow)
    {
        EnsureNotDeleted();
        EnsureValidUpdate(updatedBy, utcNow);

        var validatedName = ValidateUserName(userName);
        var validatedEmail = EmailAddress.Create(email, nameof(Email)).Value;

        UserName = validatedName;
        Email = validatedEmail;
        MarkUpdated(updatedBy, utcNow);
    }

    private static string ValidateUserName(string? userName)
    {
        var name = Guard.RequiredText(userName, nameof(UserName), UserNameMinLength, UserNameMaxLength);

        if (!UserNameFormat.IsMatch(name))
        {
            throw new DomainException(nameof(UserName),
                "UserName can only contain letters, digits, dots, underscores and hyphens.");
        }

        return name;
    }
}
