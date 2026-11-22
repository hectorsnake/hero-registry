using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Api.Tests.Security;

/// <summary>Without a valid JWT signing key the API must not start, rather than issue insecure tokens.</summary>
public class JwtStartupValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("short-key")]
    public void The_API_refuses_to_start_without_a_valid_signing_key(string key)
    {
        using var factory = new HeroRegistryApiFactory().WithWebHostBuilder(builder => builder.UseSetting("Jwt:Key", key));

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("Jwt:Key");
    }
}
