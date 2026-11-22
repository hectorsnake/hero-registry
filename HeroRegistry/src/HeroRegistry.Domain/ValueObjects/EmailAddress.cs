using System.Text.RegularExpressions;
using HeroRegistry.Domain.Common;

namespace HeroRegistry.Domain.ValueObjects;

/// <summary>A trimmed, lower-cased email address with a basic format check (name@domain.tld).</summary>
public readonly record struct EmailAddress
{
    public const int MaxLength = 256;

    // Deliberately simple: one "@", no spaces, a dot in the domain. Full RFC 5322 validation is not useful here.
    private static readonly Regex Format = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static EmailAddress Create(string? value, string field = "Email")
    {
        var email = Guard.RequiredText(value, field, minLength: 3, maxLength: MaxLength);

        if (!Format.IsMatch(email))
        {
            throw new DomainException(field, $"{field} must be a valid email address.");
        }

        return new EmailAddress(email.ToLowerInvariant());
    }

    public override string ToString() => Value;
}
