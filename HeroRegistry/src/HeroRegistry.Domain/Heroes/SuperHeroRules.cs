namespace HeroRegistry.Domain.Heroes;

/// <summary>
/// The superhero business rules as constants. They are shared by the entity, the Application
/// validators (T07) and the database CHECK constraints (T03), so the limits are defined once here.
/// </summary>
public static class SuperHeroRules
{
    public const int HeroNameMinLength = 2;
    public const int HeroNameMaxLength = 100;

    public const int PersonNameMaxLength = 100;

    public const int AddressMaxLength = 200;
    public const int CityMaxLength = 100;

    public const int PowerLevelMin = 1;
    public const int PowerLevelMax = 100;

    public const int SuperpowersMin = 1;
    public const int SuperpowersMax = 10;
    public const int SuperpowerNameMinLength = 2;
    public const int SuperpowerNameMaxLength = 50;

    /// <summary>A birth date can be at most this many years before today.</summary>
    public const int MaxAgeYears = 150;
}
