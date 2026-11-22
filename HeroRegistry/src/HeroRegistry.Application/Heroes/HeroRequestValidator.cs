using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.ValueObjects;
using static HeroRegistry.Domain.Heroes.SuperHeroRules;

namespace HeroRegistry.Application.Heroes;

/// <summary>
/// Checks a hero request against every rule of the user story and returns <b>all</b> field errors at
/// once, so the client can show them together. The domain entity enforces the same rules but stops
/// at the first one, as a last line of defense.
/// </summary>
/// <remarks>
/// The limits come from <see cref="Domain.Heroes.SuperHeroRules"/>, and the email, state and zip code
/// formats from the domain value objects, so no rule is defined twice.
/// </remarks>
public static class HeroRequestValidator
{
    /// <param name="request">The request to check.</param>
    /// <param name="today">Today's date (UTC), for the birth date rules.</param>
    /// <returns>Field name to error messages. Empty when the request is valid.</returns>
    public static IReadOnlyDictionary<string, string[]> Validate(HeroRequest request, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();

        void Add(string field, string? message)
        {
            if (message is not null)
            {
                errors[field] = [message];
            }
        }

        Add(nameof(HeroRequest.HeroName), Text(request.HeroName, nameof(HeroRequest.HeroName), HeroNameMinLength, HeroNameMaxLength));
        Add(nameof(HeroRequest.FirstName), Text(request.FirstName, nameof(HeroRequest.FirstName), 1, PersonNameMaxLength));
        Add(nameof(HeroRequest.LastName), Text(request.LastName, nameof(HeroRequest.LastName), 1, PersonNameMaxLength));
        Add(nameof(HeroRequest.BirthDate), BirthDate(request.BirthDate, today));
        Add(nameof(HeroRequest.Email), ValueObject(() => EmailAddress.Create(request.Email, nameof(HeroRequest.Email))));
        Add(nameof(HeroRequest.Superpowers), Superpowers(request.Superpowers));
        Add(nameof(HeroRequest.PowerLevel), PowerLevel(request.PowerLevel));
        Add(nameof(HeroRequest.Address1), Text(request.Address1, nameof(HeroRequest.Address1), 1, AddressMaxLength));
        Add(nameof(HeroRequest.Address2), OptionalText(request.Address2, nameof(HeroRequest.Address2), AddressMaxLength));
        Add(nameof(HeroRequest.City), Text(request.City, nameof(HeroRequest.City), 1, CityMaxLength));
        Add(nameof(HeroRequest.State), ValueObject(() => UsStateCode.Create(request.State, nameof(HeroRequest.State))));
        Add(nameof(HeroRequest.ZipCode), ValueObject(() => ZipCode.Create(request.ZipCode, nameof(HeroRequest.ZipCode))));

        return errors;
    }

    private static string? Text(string? value, string field, int min, int max)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return $"{field} is required.";
        }

        return trimmed.Length < min || trimmed.Length > max
            ? $"{field} must be between {min} and {max} characters."
            : null;
    }

    private static string? OptionalText(string? value, string field, int max) =>
        value?.Trim().Length > max ? $"{field} cannot be longer than {max} characters." : null;

    private static string? BirthDate(DateOnly? birthDate, DateOnly today) => birthDate switch
    {
        null => "BirthDate is required.",
        { } date when date >= today => "BirthDate must be in the past.",
        { } date when date < today.AddYears(-MaxAgeYears) => $"BirthDate cannot be more than {MaxAgeYears} years ago.",
        _ => null,
    };

    private static string? PowerLevel(int? powerLevel) => powerLevel switch
    {
        null => "PowerLevel is required.",
        < PowerLevelMin or > PowerLevelMax => $"PowerLevel must be between {PowerLevelMin} and {PowerLevelMax}.",
        _ => null,
    };

    private static string? Superpowers(IReadOnlyList<string>? superpowers)
    {
        if (superpowers is null || superpowers.Count == 0)
        {
            return $"A hero must have between {SuperpowersMin} and {SuperpowersMax} distinct superpowers.";
        }

        var trimmed = superpowers.Select(power => power?.Trim() ?? string.Empty).ToList();

        if (trimmed.Any(power => power.Length is < SuperpowerNameMinLength or > SuperpowerNameMaxLength))
        {
            return $"Each superpower must be between {SuperpowerNameMinLength} and {SuperpowerNameMaxLength} characters.";
        }

        var distinct = trimmed.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return distinct is < SuperpowersMin or > SuperpowersMax
            ? $"A hero must have between {SuperpowersMin} and {SuperpowersMax} distinct superpowers."
            : null;
    }

    /// <summary>Runs a value object's own validation and returns its message, if any.</summary>
    private static string? ValueObject(Action create)
    {
        try
        {
            create();
            return null;
        }
        catch (DomainException ex)
        {
            return ex.Message;
        }
    }
}
