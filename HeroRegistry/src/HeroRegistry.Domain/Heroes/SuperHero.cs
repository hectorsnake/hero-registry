using System.Collections.ObjectModel;
using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.ValueObjects;
using static HeroRegistry.Domain.Heroes.SuperHeroRules;

namespace HeroRegistry.Domain.Heroes;

/// <summary>
/// A registered superhero. It can only be created through <see cref="Create"/> and changed through
/// <see cref="Update"/> or <see cref="AuditableEntity.SoftDelete"/>, which validate and normalize every
/// value, so an instance is always valid.
/// </summary>
public sealed class SuperHero : AuditableEntity
{
    // Used by the Reflection data mapper (T05) to load a row. Application code uses Create.
    private SuperHero()
    {
    }

    public string HeroName { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public DateOnly BirthDate { get; private set; }

    /// <summary>Trimmed and lower-cased.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>1 to 10 distinct names (case-insensitive), stored as a JSON array.</summary>
    public IReadOnlyList<string> Superpowers { get; private set; } = [];

    public int PowerLevel { get; private set; }

    public string Address1 { get; private set; } = string.Empty;

    /// <summary>Optional. A blank value is stored as <see langword="null"/>.</summary>
    public string? Address2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    /// <summary>Two-letter US state code, upper-cased.</summary>
    public string State { get; private set; } = string.Empty;

    public string ZipCode { get; private set; } = string.Empty;

    /// <param name="details">The hero's details (validated and normalized).</param>
    /// <param name="createdBy">The id of the user creating the hero.</param>
    /// <param name="utcNow">The current time, in UTC. It also defines "today" for the birth date rule.</param>
    /// <exception cref="DomainException">A rule is violated.</exception>
    public static SuperHero Create(SuperHeroDetails details, int createdBy, DateTime utcNow)
    {
        var hero = new SuperHero();
        hero.MarkCreated(createdBy, utcNow);
        hero.Apply(Validate(details, utcNow));
        return hero;
    }

    /// <summary>
    /// Replaces the hero's details. All rules are checked before anything changes, so an invalid
    /// update leaves the hero untouched. The creation audit never changes.
    /// </summary>
    /// <exception cref="DomainException">A rule is violated, or the hero has been deleted.</exception>
    public void Update(SuperHeroDetails details, int updatedBy, DateTime utcNow)
    {
        EnsureNotDeleted();
        EnsureValidUpdate(updatedBy, utcNow);

        var validated = Validate(details, utcNow);

        Apply(validated);
        MarkUpdated(updatedBy, utcNow);
    }

    private void Apply(ValidatedDetails details)
    {
        HeroName = details.HeroName;
        FirstName = details.FirstName;
        LastName = details.LastName;
        BirthDate = details.BirthDate;
        Email = details.Email;
        Superpowers = details.Superpowers;
        PowerLevel = details.PowerLevel;
        Address1 = details.Address1;
        Address2 = details.Address2;
        City = details.City;
        State = details.State;
        ZipCode = details.ZipCode;
    }

    private static ValidatedDetails Validate(SuperHeroDetails details, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(details);

        return new ValidatedDetails(
            HeroName: Guard.RequiredText(details.HeroName, nameof(HeroName), HeroNameMinLength, HeroNameMaxLength),
            FirstName: Guard.RequiredText(details.FirstName, nameof(FirstName), 1, PersonNameMaxLength),
            LastName: Guard.RequiredText(details.LastName, nameof(LastName), 1, PersonNameMaxLength),
            BirthDate: ValidateBirthDate(details.BirthDate, DateOnly.FromDateTime(utcNow)),
            Email: EmailAddress.Create(details.Email, nameof(Email)).Value,
            Superpowers: NormalizeSuperpowers(details.Superpowers),
            PowerLevel: ValidatePowerLevel(details.PowerLevel),
            Address1: Guard.RequiredText(details.Address1, nameof(Address1), 1, AddressMaxLength),
            Address2: Guard.OptionalText(details.Address2, nameof(Address2), AddressMaxLength),
            City: Guard.RequiredText(details.City, nameof(City), 1, CityMaxLength),
            State: UsStateCode.Create(details.State, nameof(State)).Value,
            ZipCode: ValueObjects.ZipCode.Create(details.ZipCode, nameof(ZipCode)).Value);
    }

    private static DateOnly ValidateBirthDate(DateOnly birthDate, DateOnly today)
    {
        if (birthDate >= today)
        {
            throw new DomainException(nameof(BirthDate), "BirthDate must be in the past.");
        }

        if (birthDate < today.AddYears(-MaxAgeYears))
        {
            throw new DomainException(nameof(BirthDate), $"BirthDate cannot be more than {MaxAgeYears} years ago.");
        }

        return birthDate;
    }

    private static int ValidatePowerLevel(int powerLevel)
    {
        if (powerLevel is < PowerLevelMin or > PowerLevelMax)
        {
            throw new DomainException(nameof(PowerLevel),
                $"PowerLevel must be between {PowerLevelMin} and {PowerLevelMax}.");
        }

        return powerLevel;
    }

    /// <summary>Trims each name, removes case-insensitive duplicates (keeping the first) and checks the limits.</summary>
    private static ReadOnlyCollection<string> NormalizeSuperpowers(IReadOnlyCollection<string>? superpowers)
    {
        if (superpowers is null)
        {
            throw new DomainException(nameof(Superpowers), "Superpowers are required.");
        }

        var distinct = superpowers
            .Select(power => Guard.RequiredText(power, nameof(Superpowers), SuperpowerNameMinLength, SuperpowerNameMaxLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length is < SuperpowersMin or > SuperpowersMax)
        {
            throw new DomainException(nameof(Superpowers),
                $"A hero must have between {SuperpowersMin} and {SuperpowersMax} distinct superpowers.");
        }

        return Array.AsReadOnly(distinct);
    }

    private sealed record ValidatedDetails(
        string HeroName,
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Email,
        ReadOnlyCollection<string> Superpowers,
        int PowerLevel,
        string Address1,
        string? Address2,
        string City,
        string State,
        string ZipCode);
}
