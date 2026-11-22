using HeroRegistry.Application.Heroes;
using static HeroRegistry.Application.Tests.Heroes.HeroTestData;

namespace HeroRegistry.Application.Tests.Heroes;

/// <summary>
/// The validator returns every field error at once (the domain entity stops at the first one).
/// Its limits come from SuperHeroRules and the domain value objects.
/// </summary>
public class HeroRequestValidatorTests
{
    private static IReadOnlyDictionary<string, string[]> Validate(HeroRequest request) =>
        HeroRequestValidator.Validate(request, Today);

    [Fact]
    public void A_valid_request_has_no_errors()
    {
        Validate(ValidCreate()).ShouldBeEmpty();
        Validate(ValidUpdate()).ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_request_reports_every_required_field_together()
    {
        Validate(new CreateHeroRequest()).Keys.ShouldBe(
        [
            "HeroName", "FirstName", "LastName", "BirthDate", "Email", "Superpowers",
            "PowerLevel", "Address1", "City", "State", "ZipCode",
        ], ignoreOrder: true);
    }

    [Fact]
    public void Several_invalid_fields_are_all_reported()
    {
        var errors = Validate(ValidCreate() with { PowerLevel = 0, ZipCode = "1234", Email = "bad" });

        errors.Keys.ShouldBe(["PowerLevel", "ZipCode", "Email"], ignoreOrder: true);
    }

    // Text lengths

    [Theory]
    [InlineData("HeroName", 2, 100)]
    [InlineData("FirstName", 1, 100)]
    [InlineData("LastName", 1, 100)]
    [InlineData("Address1", 1, 200)]
    [InlineData("City", 1, 100)]
    public void Text_fields_accept_their_limits_and_reject_beyond_them(string field, int min, int max)
    {
        Validate(With(field, new string('a', min))).ShouldNotContainKey(field);
        Validate(With(field, new string('a', max))).ShouldNotContainKey(field);
        Validate(With(field, new string('a', max + 1))).ShouldContainKey(field);
        Validate(With(field, "   ")).ShouldContainKey(field);
        if (min > 1)
        {
            Validate(With(field, new string('a', min - 1))).ShouldContainKey(field);
        }
    }

    [Fact]
    public void Text_is_measured_after_trimming()
    {
        Validate(ValidCreate() with { HeroName = "  X  " }).ShouldContainKey("HeroName");
    }

    [Fact]
    public void Address2_is_optional_but_limited_to_200_characters()
    {
        Validate(ValidCreate() with { Address2 = null }).ShouldNotContainKey("Address2");
        Validate(ValidCreate() with { Address2 = new string('a', 200) }).ShouldNotContainKey("Address2");
        Validate(ValidCreate() with { Address2 = new string('a', 201) }).ShouldContainKey("Address2");
    }

    // Power level

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void PowerLevel_must_be_between_1_and_100(int powerLevel, bool invalid)
    {
        Validate(ValidCreate() with { PowerLevel = powerLevel }).ContainsKey("PowerLevel").ShouldBe(invalid);
    }

    // Birth date

    [Fact]
    public void BirthDate_must_be_in_the_past_and_at_most_150_years_ago()
    {
        Validate(ValidCreate() with { BirthDate = Today.AddDays(-1) }).ShouldNotContainKey("BirthDate");
        Validate(ValidCreate() with { BirthDate = Today }).ShouldContainKey("BirthDate");
        Validate(ValidCreate() with { BirthDate = Today.AddDays(1) }).ShouldContainKey("BirthDate");
        Validate(ValidCreate() with { BirthDate = Today.AddYears(-150) }).ShouldNotContainKey("BirthDate");
        Validate(ValidCreate() with { BirthDate = Today.AddYears(-150).AddDays(-1) }).ShouldContainKey("BirthDate");
    }

    // Superpowers

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(10, false)]
    [InlineData(11, true)]
    public void One_to_ten_superpowers_are_required(int count, bool invalid)
    {
        var powers = Enumerable.Range(1, count).Select(i => $"Power {i}").ToArray();

        Validate(ValidCreate() with { Superpowers = powers }).ContainsKey("Superpowers").ShouldBe(invalid);
    }

    [Fact]
    public void The_superpower_limit_counts_distinct_names_ignoring_case()
    {
        var powers = Enumerable.Range(1, 10).Select(i => $"Power {i}").Append("POWER 1").ToArray();

        Validate(ValidCreate() with { Superpowers = powers }).ShouldNotContainKey("Superpowers");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51
    public void Each_superpower_must_have_2_to_50_characters(string power)
    {
        Validate(ValidCreate() with { Superpowers = ["Flight", power] }).ShouldContainKey("Superpowers");
    }

    [Fact]
    public void A_null_superpower_name_is_rejected()
    {
        Validate(ValidCreate() with { Superpowers = ["Flight", null!] }).ShouldContainKey("Superpowers");
    }

    // Email, state and zip code (rules shared with the domain value objects)

    [Theory]
    [InlineData("Email", "not-an-email")]
    [InlineData("Email", "a@b")]
    [InlineData("State", "XX")]
    [InlineData("State", "N")]
    [InlineData("ZipCode", "1234")]
    [InlineData("ZipCode", "12345-678")]
    public void Email_state_and_zip_code_formats_are_checked(string field, string value)
    {
        Validate(With(field, value)).ShouldContainKey(field);
    }

    [Theory]
    [InlineData("State", "ny")]
    [InlineData("ZipCode", "12345-6789")]
    [InlineData("Email", " Natasha@Avengers.org ")]
    public void Values_the_domain_normalizes_are_accepted(string field, string value)
    {
        Validate(With(field, value)).ShouldNotContainKey(field);
    }

    [Fact]
    public void Each_field_gets_a_readable_message()
    {
        Validate(ValidCreate() with { PowerLevel = 101 })["PowerLevel"].ShouldBe(["PowerLevel must be between 1 and 100."]);
    }

    private static CreateHeroRequest With(string field, string? value) => field switch
    {
        "HeroName" => ValidCreate() with { HeroName = value },
        "FirstName" => ValidCreate() with { FirstName = value },
        "LastName" => ValidCreate() with { LastName = value },
        "Email" => ValidCreate() with { Email = value },
        "Address1" => ValidCreate() with { Address1 = value },
        "City" => ValidCreate() with { City = value },
        "State" => ValidCreate() with { State = value },
        "ZipCode" => ValidCreate() with { ZipCode = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };
}
