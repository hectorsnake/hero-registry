using HeroRegistry.Application.Heroes;
using HeroRegistry.Domain.Heroes;

namespace HeroRegistry.Application.Tests.Heroes;

internal static class HeroTestData
{
    public const int AdminId = 1;
    public const int AgentId = 2;

    /// <summary>A fixed "now", so date rules are deterministic.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    public static CreateHeroRequest ValidCreate() => new()
    {
        HeroName = "Black Widow",
        FirstName = "Natasha",
        LastName = "Romanoff",
        BirthDate = new DateOnly(1984, 11, 22),
        Email = "black.widow@heroregistry.com",
        Superpowers = ["Espionage", "Martial arts"],
        PowerLevel = 75,
        Address1 = "1 Avengers Tower",
        Address2 = null,
        City = "Manhattan",
        State = "NY",
        ZipCode = "10001",
    };

    public static UpdateHeroRequest ValidUpdate() => new()
    {
        HeroName = "Black Widow",
        FirstName = "Natasha",
        LastName = "Romanoff",
        BirthDate = new DateOnly(1984, 11, 22),
        Email = "black.widow@heroregistry.com",
        Superpowers = ["Espionage", "Martial arts", "Marksmanship"],
        PowerLevel = 80,
        Address1 = "1 Avengers Tower",
        Address2 = "Floor 93",
        City = "Manhattan",
        State = "NY",
        ZipCode = "10001",
    };

    /// <summary>An existing hero as the repository would return it (Id assigned).</summary>
    public static SuperHero ExistingHero(int id = 7, string heroName = "Black Widow", string email = "black.widow@heroregistry.com")
    {
        var hero = SuperHero.Create(
            new SuperHeroDetails(heroName, "Natasha", "Romanoff", new DateOnly(1984, 11, 22), email,
                ["Espionage"], 75, "1 Avengers Tower", null, "Manhattan", "NY", "10001"),
            AdminId,
            Now.UtcDateTime.AddDays(-30));

        typeof(SuperHero).GetProperty(nameof(SuperHero.Id))!.SetValue(hero, id);
        return hero;
    }
}
