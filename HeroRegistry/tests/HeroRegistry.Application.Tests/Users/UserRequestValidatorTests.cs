using HeroRegistry.Application.Users;
using static HeroRegistry.Application.Tests.Users.UserTestData;

namespace HeroRegistry.Application.Tests.Users;

public class RegisterUserRequestValidatorTests
{
    private static IReadOnlyDictionary<string, string[]> Validate(RegisterUserRequest request) =>
        UserRequestValidator.Validate(request);

    [Fact]
    public void A_valid_registration_has_no_errors()
    {
        Validate(ValidRegistration()).ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_registration_reports_every_field_together()
    {
        Validate(new RegisterUserRequest()).Keys.ShouldBe(["UserName", "Email", "Password"], ignoreOrder: true);
    }

    // User name: 3 to 50 characters, letters, digits, dot, underscore and hyphen

    [Theory]
    [InlineData("abc")]
    [InlineData("peter.parker_99-x")]
    [InlineData("12345678901234567890123456789012345678901234567890")] // 50
    public void Valid_user_names_are_accepted(string userName)
    {
        Validate(ValidRegistration() with { UserName = userName }).ShouldNotContainKey("UserName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData("peter parker")]
    [InlineData("peter@parker")]
    [InlineData("pétér")]
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51
    public void Invalid_user_names_are_rejected(string? userName)
    {
        Validate(ValidRegistration() with { UserName = userName }).ShouldContainKey("UserName");
    }

    // Email (rules of the domain EmailAddress value object)

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-email")]
    [InlineData("peter@dailybugle")]
    public void Invalid_emails_are_rejected(string? email)
    {
        Validate(ValidRegistration() with { Email = email }).ShouldContainKey("Email");
    }

    // Password policy: 8 to 64 characters, upper, lower, digit and symbol

    [Theory]
    [InlineData("Aa1!aaaa")]            // exactly 8
    [InlineData("Sp1der-Sense!")]
    [InlineData("Correct Horse 9 Battery Staple!")] // spaces count as symbols
    public void Strong_passwords_are_accepted(string password)
    {
        Validate(ValidRegistration() with { Password = password }).ShouldNotContainKey("Password");
    }

    [Theory]
    [InlineData(null, "Password is required.")]
    [InlineData("", "Password is required.")]
    [InlineData("Aa1!aaa", "Password must be between 8 and 64 characters.")]
    [InlineData("aa1!aaaa", "Password must contain an uppercase letter.")]
    [InlineData("AA1!AAAA", "Password must contain a lowercase letter.")]
    [InlineData("Aaa!aaaa", "Password must contain a digit.")]
    [InlineData("Aaa1aaaa", "Password must contain a symbol.")]
    public void Weak_passwords_are_rejected_with_the_reason(string? password, string message)
    {
        Validate(ValidRegistration() with { Password = password })["Password"].ShouldContain(message);
    }

    [Fact]
    public void Every_missing_password_requirement_is_listed()
    {
        Validate(ValidRegistration() with { Password = "aaaaaaaa" })["Password"].Length.ShouldBe(3);
    }

    [Fact]
    public void Passwords_longer_than_64_characters_are_rejected()
    {
        Validate(ValidRegistration() with { Password = "Aa1!" + new string('a', 61) }).ShouldContainKey("Password");
    }

    [Fact]
    public void Passwords_over_72_UTF8_bytes_are_rejected_because_BCrypt_would_ignore_the_rest()
    {
        // 40 characters, but 'é' takes 2 bytes in UTF-8: 4 + 36 * 2 = 76 bytes.
        var password = "Aa1!" + new string('é', 36);

        Validate(ValidRegistration() with { Password = password })["Password"]
            .ShouldContain("Password is too long once encoded (72 bytes maximum).");
    }

    [Fact]
    public void The_password_is_never_trimmed_or_echoed_in_messages()
    {
        var errors = Validate(ValidRegistration() with { Password = " weak " });

        errors["Password"].ShouldAllBe(message => !message.Contains("weak"));
    }
}

public class LoginRequestValidatorTests
{
    [Fact]
    public void A_complete_login_has_no_errors()
    {
        UserRequestValidator.Validate(new LoginRequest { UserName = "Admin", Password = "Admin" }).ShouldBeEmpty();
    }

    [Fact]
    public void Login_only_requires_both_fields_and_never_applies_the_password_policy()
    {
        // The seeded "Admin" password is weaker than the policy and must still be able to log in.
        UserRequestValidator.Validate(new LoginRequest { UserName = "Admin", Password = "x" }).ShouldBeEmpty();
        UserRequestValidator.Validate(new LoginRequest()).Keys.ShouldBe(["UserName", "Password"], ignoreOrder: true);
    }
}

public class SensitiveRequestTests
{
    [Fact]
    public void A_registration_request_never_prints_its_password()
    {
        var text = ValidRegistration().ToString();

        text.ShouldNotContain(StrongPassword);
        text.ShouldContain("peter.parker");
        text.ShouldContain("Password = ***");
    }

    [Fact]
    public void A_login_request_never_prints_its_password()
    {
        new LoginRequest { UserName = "Admin", Password = "Admin-secret" }.ToString().ShouldNotContain("Admin-secret");
    }
}
