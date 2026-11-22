using System.Text.RegularExpressions;
using HeroRegistry.Domain.Common;

namespace HeroRegistry.Domain.ValueObjects;

/// <summary>A US ZIP code: <c>12345</c> or ZIP+4 <c>12345-6789</c>.</summary>
public readonly record struct ZipCode
{
    public const int MaxLength = 10;

    // [0-9] rather than \d, which would also accept non-ASCII digits.
    private static readonly Regex Format = new(
        "^[0-9]{5}(-[0-9]{4})?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private ZipCode(string value) => Value = value;

    public string Value { get; }

    public static ZipCode Create(string? value, string field = "ZipCode")
    {
        var zip = value?.Trim();

        if (string.IsNullOrEmpty(zip) || !Format.IsMatch(zip))
        {
            throw new DomainException(field, $"{field} must be 5 digits (12345) or ZIP+4 (12345-6789).");
        }

        return new ZipCode(zip);
    }

    public override string ToString() => Value;
}
