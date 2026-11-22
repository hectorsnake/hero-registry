using HeroRegistry.Application.Abstractions.Security;
using HeroRegistry.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests.Security;

/// <summary>
/// JWT settings come from the "Jwt" section (the key from user secrets, Aspire or .env) and are
/// validated at startup, so a missing or weak key stops the API instead of issuing insecure tokens.
/// </summary>
public class JwtOptionsTests
{
    private const string ValidKey = "a-signing-key-that-is-at-least-32-bytes";

    [Fact]
    public void Binds_the_Jwt_section_with_sensible_defaults()
    {
        var options = OptionsFrom(("Jwt:Key", ValidKey));

        options.Key.ShouldBe(ValidKey);
        options.Issuer.ShouldBe("hero-registry-api");
        options.Audience.ShouldBe("hero-registry-ui");
        options.ExpirationMinutes.ShouldBe(60);
    }

    [Fact]
    public void Every_setting_can_be_configured()
    {
        var options = OptionsFrom(
            ("Jwt:Key", ValidKey), ("Jwt:Issuer", "issuer"), ("Jwt:Audience", "audience"), ("Jwt:ExpirationMinutes", "15"));

        options.Issuer.ShouldBe("issuer");
        options.Audience.ShouldBe("audience");
        options.ExpirationMinutes.ShouldBe(15);
    }

    [Fact]
    public void A_missing_key_is_rejected_with_instructions()
    {
        var exception = Should.Throw<OptionsValidationException>(() => OptionsFrom());

        exception.Message.ShouldContain("Jwt:Key");
        exception.Message.ShouldContain("user-secrets");
    }

    [Fact]
    public void A_key_shorter_than_32_bytes_is_rejected()
    {
        Should.Throw<OptionsValidationException>(() => OptionsFrom(("Jwt:Key", "too-short")))
            .Message.ShouldContain("32 bytes");
    }

    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", " ")]
    [InlineData("Jwt:ExpirationMinutes", "0")]
    [InlineData("Jwt:ExpirationMinutes", "1441")]
    public void Invalid_settings_are_rejected(string key, string value)
    {
        Should.Throw<OptionsValidationException>(() => OptionsFrom(("Jwt:Key", ValidKey), (key, value)));
    }

    [Fact]
    public void The_hasher_and_token_service_are_registered_as_the_Application_ports()
    {
        using var provider = Provider(("Jwt:Key", ValidKey));

        provider.GetRequiredService<IPasswordHasher>().ShouldBeOfType<BCryptPasswordHasher>();
        provider.GetRequiredService<ITokenService>().ShouldBeOfType<JwtTokenService>();
    }

    private static JwtOptions OptionsFrom(params (string Key, string? Value)[] settings)
    {
        using var provider = Provider(settings);
        return provider.GetRequiredService<IOptions<JwtOptions>>().Value;
    }

    private static ServiceProvider Provider(params (string Key, string? Value)[] settings) =>
        new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(new ConfigurationBuilder()
                .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
                .Build())
            .BuildServiceProvider();
}
