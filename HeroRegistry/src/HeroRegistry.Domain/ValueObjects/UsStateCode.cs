using HeroRegistry.Domain.Common;

namespace HeroRegistry.Domain.ValueObjects;

/// <summary>A two-letter USPS code for a US state, the District of Columbia or an inhabited territory.</summary>
public readonly record struct UsStateCode
{
    private static readonly HashSet<string> Codes =
    [
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
        "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
        "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
        "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
        "DC", // District of Columbia
        "AS", "GU", "MP", "PR", "VI", // territories
    ];

    private UsStateCode(string value) => Value = value;

    public string Value { get; }

    /// <summary>All accepted codes, for example to build a drop-down list.</summary>
    public static IReadOnlyCollection<string> All => Codes;

    public static UsStateCode Create(string? value, string field = "State")
    {
        var code = value?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(code) || !Codes.Contains(code))
        {
            throw new DomainException(field, $"{field} must be a valid two-letter US state code.");
        }

        return new UsStateCode(code);
    }

    public override string ToString() => Value;
}
