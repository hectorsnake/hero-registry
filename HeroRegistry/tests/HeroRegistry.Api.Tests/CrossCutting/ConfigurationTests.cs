using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Api.Tests.CrossCutting;

/// <summary>Startup validation of every options class, health endpoints in all environments, JSON settings.</summary>
public class ConfigurationTests(HeroRegistryApiFactory factory) : IClassFixture<HeroRegistryApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Database:MigrationMaxAttempts", "0")]
    [InlineData("Database:SlowCommandThresholdMs", "-1")]
    public void Invalid_database_settings_stop_the_API_at_startup(string key, string value)
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        Should.Throw<OptionsValidationException>(() => configured.CreateClient());
    }

    [Fact]
    public async Task Health_endpoints_are_available_anonymously_outside_Development_for_container_probes()
    {
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));
        using var client = production.CreateClient();

        using var alive = await client.GetAsync("/alive", Ct);
        using var health = await client.GetAsync("/health", Ct);

        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
        health.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable); // no database in this test
        (await health.Content.ReadAsStringAsync(Ct)).ShouldBe("Unhealthy"); // status only, no details
    }

    [Fact]
    public void Enums_are_serialized_as_strings_by_controllers_and_minimal_endpoints()
    {
        factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value
            .JsonSerializerOptions.Converters.ShouldContain(c => c is JsonStringEnumConverter);
        factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value
            .SerializerOptions.Converters.ShouldContain(c => c is JsonStringEnumConverter);
    }
}
