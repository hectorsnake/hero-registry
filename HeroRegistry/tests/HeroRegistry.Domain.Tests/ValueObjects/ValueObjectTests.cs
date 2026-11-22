using HeroRegistry.Domain.Common;
using HeroRegistry.Domain.ValueObjects;

namespace HeroRegistry.Domain.Tests.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("superman@heroregistry.com", "superman@heroregistry.com")]
    [InlineData("  Clark.Kent@DailyPlanet.com  ", "clark.kent@dailyplanet.com")]
    [InlineData("a+tag@sub.example.org", "a+tag@sub.example.org")]
    public void Valid_emails_are_trimmed_and_lower_cased(string input, string expected)
    {
        EmailAddress.Create(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("superman")]
    [InlineData("superman@")]
    [InlineData("@heroregistry.com")]
    [InlineData("super man@heroregistry.com")]
    [InlineData("superman@heroregistry")]
    [InlineData("a@b@heroregistry.com")]
    public void Invalid_emails_are_rejected(string? input)
    {
        Should.Throw<DomainException>(() => EmailAddress.Create(input)).Field.ShouldBe("Email");
    }

    [Fact]
    public void Emails_longer_than_256_characters_are_rejected()
    {
        var tooLong = new string('a', 245) + "@example.com"; // 257 characters

        Should.Throw<DomainException>(() => EmailAddress.Create(tooLong));
    }

    [Fact]
    public void The_field_name_can_be_customized_for_error_messages()
    {
        Should.Throw<DomainException>(() => EmailAddress.Create("bad", field: "ContactEmail")).Field.ShouldBe("ContactEmail");
    }
}

public class UsStateCodeTests
{
    [Theory]
    [InlineData("NY", "NY")]
    [InlineData(" ny ", "NY")]
    [InlineData("ca", "CA")]
    [InlineData("DC", "DC")]
    [InlineData("PR", "PR")]
    public void Valid_codes_are_trimmed_and_upper_cased(string input, string expected)
    {
        UsStateCode.Create(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("N")]
    [InlineData("NYC")]
    [InlineData("XX")]
    [InlineData("12")]
    public void Unknown_codes_are_rejected(string? input)
    {
        Should.Throw<DomainException>(() => UsStateCode.Create(input)).Field.ShouldBe("State");
    }
}

public class ZipCodeTests
{
    [Theory]
    [InlineData("11201", "11201")]
    [InlineData(" 11201-1234 ", "11201-1234")]
    public void Five_digit_and_ZIP_plus_4_codes_are_accepted(string input, string expected)
    {
        ZipCode.Create(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("12345-678")]
    [InlineData("12345 6789")]
    [InlineData("ABCDE")]
    public void Other_formats_are_rejected(string? input)
    {
        Should.Throw<DomainException>(() => ZipCode.Create(input)).Field.ShouldBe("ZipCode");
    }
}
