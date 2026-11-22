using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// The health endpoints come from ServiceDefaults. Aspire polls /health to show the API's state.
/// No database is configured in these tests, so readiness must report the API as unhealthy.
/// </summary>
public class HealthEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Alive_returns_ok_without_checking_dependencies()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/alive", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_includes_the_database_and_is_unavailable_without_one()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Unhealthy");
    }
}
