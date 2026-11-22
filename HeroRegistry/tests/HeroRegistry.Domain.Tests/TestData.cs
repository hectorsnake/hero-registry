using HeroRegistry.Domain.Heroes;

namespace HeroRegistry.Domain.Tests;

/// <summary>Valid sample data. Tests change one field at a time with <c>with { ... }</c>.</summary>
internal static class TestData
{
    public const int AdminId = 1;
    public const int OtherUserId = 2;

    /// <summary>A fixed "now" (UTC), so date rules are deterministic.</summary>
    public static readonly DateTime UtcNow = new(2026, 11, 22, 15, 30, 0, DateTimeKind.Utc);

    public static readonly DateOnly Today = DateOnly.FromDateTime(UtcNow);

    public static SuperHeroDetails Hero() => new(
        HeroName: "Captain America",
        FirstName: "Steve",
        LastName: "Rogers",
        BirthDate: new DateOnly(1918, 7, 4),
        Email: "captain.america@heroregistry.com",
        Superpowers: ["Super-soldier strength", "Shield combat"],
        PowerLevel: 85,
        Address1: "569 Leaman Place",
        Address2: null,
        City: "Brooklyn",
        State: "NY",
        ZipCode: "11201");

    public static SuperHero CreateHero(SuperHeroDetails? details = null) =>
        SuperHero.Create(details ?? Hero(), AdminId, UtcNow);
}
