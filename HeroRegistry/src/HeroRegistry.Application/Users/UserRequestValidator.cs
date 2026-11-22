using System.Text;
using System.Text.RegularExpressions;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.ValueObjects;
using static HeroRegistry.Domain.Users.UserRules;

namespace HeroRegistry.Application.Users;

/// <summary>
/// Validates registration and login requests and returns <b>all</b> field errors at once. Limits come
/// from <see cref="Domain.Users.UserRules"/> and the email format from the domain value object.
/// Error messages never repeat the password.
/// </summary>
public static class UserRequestValidator
{
    private static readonly Regex UserNameFormat = new(UserNamePattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static IReadOnlyDictionary<string, string[]> Validate(RegisterUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();
        AddIfAny(errors, nameof(RegisterUserRequest.UserName), UserName(request.UserName));
        AddIfAny(errors, nameof(RegisterUserRequest.Email), Email(request.Email));
        AddIfAny(errors, nameof(RegisterUserRequest.Password), PasswordPolicy(request.Password));
        return errors;
    }

    /// <summary>
    /// Login only checks that both fields are present. The password policy applies to new passwords,
    /// not to existing ones (the seeded "Admin" password is weaker than the policy).
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Validate(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            errors[nameof(LoginRequest.UserName)] = ["UserName is required."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors[nameof(LoginRequest.Password)] = ["Password is required."];
        }

        return errors;
    }

    private static List<string> UserName(string? userName)
    {
        var name = userName?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return ["UserName is required."];
        }

        if (name.Length is < UserNameMinLength or > UserNameMaxLength)
        {
            return [$"UserName must be between {UserNameMinLength} and {UserNameMaxLength} characters."];
        }

        return UserNameFormat.IsMatch(name)
            ? []
            : ["UserName can only contain letters, digits, dots, underscores and hyphens."];
    }

    private static List<string> Email(string? email)
    {
        try
        {
            EmailAddress.Create(email, nameof(RegisterUserRequest.Email));
            return [];
        }
        catch (DomainException ex)
        {
            return [ex.Message];
        }
    }

    /// <summary>Every unmet requirement is listed. The password is used exactly as typed (never trimmed).</summary>
    private static List<string> PasswordPolicy(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return ["Password is required."];
        }

        var problems = new List<string>();

        if (password.Length is < PasswordMinLength or > PasswordMaxLength)
        {
            problems.Add($"Password must be between {PasswordMinLength} and {PasswordMaxLength} characters.");
        }
        else if (Encoding.UTF8.GetByteCount(password) > PasswordMaxBytes)
        {
            problems.Add($"Password is too long once encoded ({PasswordMaxBytes} bytes maximum).");
        }

        if (!password.Any(char.IsUpper))
        {
            problems.Add("Password must contain an uppercase letter.");
        }

        if (!password.Any(char.IsLower))
        {
            problems.Add("Password must contain a lowercase letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            problems.Add("Password must contain a digit.");
        }

        if (password.All(char.IsLetterOrDigit))
        {
            problems.Add("Password must contain a symbol.");
        }

        return problems;
    }

    private static void AddIfAny(Dictionary<string, string[]> errors, string field, List<string> messages)
    {
        if (messages.Count > 0)
        {
            errors[field] = [.. messages];
        }
    }
}
