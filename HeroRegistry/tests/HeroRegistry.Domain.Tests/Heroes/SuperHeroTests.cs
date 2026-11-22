using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.Heroes;
using static HeroRegistry.Domain.Tests.TestData;

namespace HeroRegistry.Domain.Tests.Heroes;

public class SuperHeroCreateTests
{
    [Fact]
    public void A_valid_hero_is_created_with_all_its_details()
    {
        var hero = CreateHero();

        hero.Id.ShouldBe(0); // assigned by the database
        hero.HeroName.ShouldBe("Captain America");
        hero.FirstName.ShouldBe("Steve");
        hero.LastName.ShouldBe("Rogers");
        hero.BirthDate.ShouldBe(new DateOnly(1918, 7, 4));
        hero.Email.ShouldBe("captain.america@heroregistry.com");
        hero.Superpowers.ShouldBe(["Super-soldier strength", "Shield combat"]);
        hero.PowerLevel.ShouldBe(85);
        hero.Address1.ShouldBe("569 Leaman Place");
        hero.Address2.ShouldBeNull();
        hero.City.ShouldBe("Brooklyn");
        hero.State.ShouldBe("NY");
        hero.ZipCode.ShouldBe("11201");
    }

    // Normalization

    [Fact]
    public void Text_is_trimmed_the_state_upper_cased_and_the_email_lower_cased()
    {
        var hero = CreateHero(Hero() with
        {
            HeroName = "  Captain America ",
            FirstName = " Steve ",
            LastName = " Rogers ",
            Email = " Captain.America@HeroRegistry.com ",
            Address1 = " 569 Leaman Place ",
            Address2 = " Apt 1 ",
            City = " Brooklyn ",
            State = " ny ",
            ZipCode = " 11201 ",
        });

        hero.HeroName.ShouldBe("Captain America");
        hero.FirstName.ShouldBe("Steve");
        hero.LastName.ShouldBe("Rogers");
        hero.Email.ShouldBe("captain.america@heroregistry.com");
        hero.Address1.ShouldBe("569 Leaman Place");
        hero.Address2.ShouldBe("Apt 1");
        hero.City.ShouldBe("Brooklyn");
        hero.State.ShouldBe("NY");
        hero.ZipCode.ShouldBe("11201");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_Address2_is_stored_as_null(string? address2)
    {
        CreateHero(Hero() with { Address2 = address2 }).Address2.ShouldBeNull();
    }

    [Fact]
    public void Superpowers_are_trimmed_and_duplicates_removed_ignoring_case()
    {
        var hero = CreateHero(Hero() with { Superpowers = [" Flight ", "flight", "Heat vision", "FLIGHT"] });

        hero.Superpowers.ShouldBe(["Flight", "Heat vision"]);
    }

    [Fact]
    public void Superpowers_cannot_be_changed_from_outside()
    {
        var powers = new List<string> { "Flight" };
        var hero = CreateHero(Hero() with { Superpowers = powers });

        powers.Add("Heat vision");

        hero.Superpowers.ShouldBe(["Flight"]);
        (hero.Superpowers as ICollection<string>)?.IsReadOnly.ShouldBeTrue();
    }

    // Required text fields and lengths

    [Theory]
    [InlineData("HeroName", 2, 100)]
    [InlineData("FirstName", 1, 100)]
    [InlineData("LastName", 1, 100)]
    [InlineData("Address1", 1, 200)]
    [InlineData("City", 1, 100)]
    public void Required_text_fields_accept_their_length_limits(string field, int min, int max)
    {
        Should.NotThrow(() => CreateHero(With(field, new string('a', min))));
        Should.NotThrow(() => CreateHero(With(field, new string('a', max))));
    }

    [Theory]
    [InlineData("HeroName", 2, 100)]
    [InlineData("FirstName", 1, 100)]
    [InlineData("LastName", 1, 100)]
    [InlineData("Address1", 1, 200)]
    [InlineData("City", 1, 100)]
    public void Required_text_fields_reject_blank_too_short_or_too_long_values(string field, int min, int max)
    {
        ShouldFailOn(field, () => CreateHero(With(field, null)));
        ShouldFailOn(field, () => CreateHero(With(field, "   ")));
        ShouldFailOn(field, () => CreateHero(With(field, new string('a', max + 1))));
        if (min > 1)
        {
            ShouldFailOn(field, () => CreateHero(With(field, new string('a', min - 1))));
        }
    }

    [Fact]
    public void Address2_is_limited_to_200_characters()
    {
        Should.NotThrow(() => CreateHero(Hero() with { Address2 = new string('a', 200) }));
        ShouldFailOn("Address2", () => CreateHero(Hero() with { Address2 = new string('a', 201) }));
    }

    // Power level

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void PowerLevel_boundaries_are_accepted(int powerLevel)
    {
        CreateHero(Hero() with { PowerLevel = powerLevel }).PowerLevel.ShouldBe(powerLevel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-5)]
    public void PowerLevel_outside_1_to_100_is_rejected(int powerLevel)
    {
        ShouldFailOn("PowerLevel", () => CreateHero(Hero() with { PowerLevel = powerLevel }));
    }

    // Birth date

    [Fact]
    public void BirthDate_must_be_in_the_past()
    {
        Should.NotThrow(() => CreateHero(Hero() with { BirthDate = Today.AddDays(-1) }));
        ShouldFailOn("BirthDate", () => CreateHero(Hero() with { BirthDate = Today }));
        ShouldFailOn("BirthDate", () => CreateHero(Hero() with { BirthDate = Today.AddDays(1) }));
    }

    [Fact]
    public void BirthDate_cannot_be_more_than_150_years_ago()
    {
        Should.NotThrow(() => CreateHero(Hero() with { BirthDate = Today.AddYears(-150) }));
        ShouldFailOn("BirthDate", () => CreateHero(Hero() with { BirthDate = Today.AddYears(-150).AddDays(-1) }));
    }

    // Superpowers

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void One_to_ten_superpowers_are_accepted(int count)
    {
        CreateHero(Hero() with { Superpowers = Powers(count) }).Superpowers.Count.ShouldBe(count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Fewer_than_one_or_more_than_ten_superpowers_are_rejected(int count)
    {
        ShouldFailOn("Superpowers", () => CreateHero(Hero() with { Superpowers = Powers(count) }));
    }

    [Fact]
    public void The_superpower_limit_counts_distinct_powers()
    {
        var elevenWithOneDuplicate = Powers(10).Append("power 1").ToArray();

        CreateHero(Hero() with { Superpowers = elevenWithOneDuplicate }).Superpowers.Count.ShouldBe(10);
    }

    [Fact]
    public void A_missing_superpowers_list_is_rejected()
    {
        ShouldFailOn("Superpowers", () => CreateHero(Hero() with { Superpowers = null! }));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("12345678901234567890123456789012345678901234567890")] // 50
    public void Each_superpower_accepts_2_to_50_characters(string power)
    {
        Should.NotThrow(() => CreateHero(Hero() with { Superpowers = [power] }));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51
    public void Each_superpower_rejects_blank_too_short_or_too_long_names(string power)
    {
        ShouldFailOn("Superpowers", () => CreateHero(Hero() with { Superpowers = ["Flight", power] }));
    }

    // Value-object fields

    [Theory]
    [InlineData("Email", "not-an-email")]
    [InlineData("State", "XX")]
    [InlineData("ZipCode", "1234")]
    public void Email_state_and_zip_code_are_validated(string field, string value)
    {
        ShouldFailOn(field, () => CreateHero(With(field, value)));
    }

    // T03.1 follow-up: the demo seed data (0003_SeedData.sql) must satisfy the domain rules too.

    public static TheoryData<SuperHeroDetails> SeededHeroes() =>
    [
        new SuperHeroDetails("Captain America", "Steve", "Rogers", new DateOnly(1918, 7, 4), "captain.america@heroregistry.com",
            ["Super-soldier strength", "Enhanced agility", "Shield combat", "Tactical leadership"], 85,
            "569 Leaman Place", null, "Brooklyn", "NY", "11201"),
        new SuperHeroDetails("Spider-Man", "Peter", "Parker", new DateOnly(2001, 8, 10), "spider-man@heroregistry.com",
            ["Wall-crawling", "Spider-sense", "Superhuman strength", "Web-slinging"], 80,
            "20 Ingram Street", null, "Queens", "NY", "11375"),
        new SuperHeroDetails("Superman", "Clark", "Kent", new DateOnly(1977, 6, 18), "superman@heroregistry.com",
            ["Flight", "Super strength", "Heat vision", "Invulnerability", "X-ray vision"], 100,
            "344 Clinton Street", "Apt 3D", "Metropolis", "NY", "10001"),
    ];

    [Theory]
    [MemberData(nameof(SeededHeroes))]
    public void The_seeded_heroes_satisfy_the_domain_rules_unchanged(SuperHeroDetails seeded)
    {
        var hero = CreateHero(seeded);

        // Already normalized: the seed script stores exactly what the domain would.
        hero.HeroName.ShouldBe(seeded.HeroName);
        hero.Email.ShouldBe(seeded.Email);
        hero.Superpowers.ShouldBe(seeded.Superpowers);
        hero.State.ShouldBe(seeded.State);
    }

    internal static SuperHeroDetails With(string field, string? value) => field switch
    {
        "HeroName" => Hero() with { HeroName = value! },
        "FirstName" => Hero() with { FirstName = value! },
        "LastName" => Hero() with { LastName = value! },
        "Email" => Hero() with { Email = value! },
        "Address1" => Hero() with { Address1 = value! },
        "City" => Hero() with { City = value! },
        "State" => Hero() with { State = value! },
        "ZipCode" => Hero() with { ZipCode = value! },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    internal static string[] Powers(int count) =>
        Enumerable.Range(1, count).Select(i => $"Power {i}").ToArray();

    internal static void ShouldFailOn(string field, Action action) =>
        Should.Throw<DomainException>(action).Field.ShouldBe(field);
}

public class SuperHeroUpdateTests
{
    private static readonly DateTime Later = UtcNow.AddDays(1);

    [Fact]
    public void Update_replaces_the_details_and_records_who_and_when()
    {
        var hero = CreateHero();

        hero.Update(Hero() with { PowerLevel = 90, City = " Manhattan ", Superpowers = ["Shield combat"] }, OtherUserId, Later);

        hero.PowerLevel.ShouldBe(90);
        hero.City.ShouldBe("Manhattan");
        hero.Superpowers.ShouldBe(["Shield combat"]);
        hero.UpdatedBy.ShouldBe(OtherUserId);
        hero.UpdatedAt.ShouldBe(Later);
    }

    [Fact]
    public void Update_never_changes_the_creation_audit()
    {
        var hero = CreateHero();

        hero.Update(Hero() with { PowerLevel = 90 }, OtherUserId, Later);

        hero.CreatedBy.ShouldBe(AdminId);
        hero.CreatedAt.ShouldBe(UtcNow);
    }

    [Fact]
    public void Update_applies_the_same_rules_and_leaves_the_hero_unchanged_when_invalid()
    {
        var hero = CreateHero();

        SuperHeroCreateTests.ShouldFailOn("PowerLevel",
            () => hero.Update(Hero() with { HeroName = "Renamed", PowerLevel = 0 }, OtherUserId, Later));

        hero.HeroName.ShouldBe("Captain America");
        hero.PowerLevel.ShouldBe(85);
        hero.UpdatedBy.ShouldBeNull();
    }

    [Fact]
    public void Birth_date_is_checked_against_the_update_date()
    {
        var hero = CreateHero();
        var tomorrow = DateOnly.FromDateTime(Later).AddDays(1);

        SuperHeroCreateTests.ShouldFailOn("BirthDate",
            () => hero.Update(Hero() with { BirthDate = tomorrow }, OtherUserId, Later));
    }

    [Fact]
    public void A_deleted_hero_cannot_be_updated()
    {
        var hero = CreateHero();
        hero.SoftDelete(AdminId, Later);

        Should.Throw<DomainException>(() => hero.Update(Hero(), AdminId, Later.AddMinutes(1)));
    }
}
