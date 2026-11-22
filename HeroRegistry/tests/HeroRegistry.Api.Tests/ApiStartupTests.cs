using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// Smoke tests: the composition root (Application, Infrastructure and Api) builds and serves requests.
/// </summary>
public class ApiStartupTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task OpenApi_document_is_served_in_Development()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Removed_template_endpoint_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/weatherforecast", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
