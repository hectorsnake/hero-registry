using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// Smoke tests: the composition root (Application, Infrastructure and Api) builds and serves requests.
/// </summary>
public class ApiStartupTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // OpenAPI and Scalar behavior is covered in OpenApiAndScalarTests (T02.1).

    [Fact]
    public async Task Removed_template_endpoint_returns_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/weatherforecast", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
