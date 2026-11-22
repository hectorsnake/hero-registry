using System.Net;
using System.Net.Http.Headers;
using HeroRegistry.Api.Tests.Fixtures;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// Smoke tests: the composition root (Application, Infrastructure and Api) builds and serves requests.
/// </summary>
public class ApiStartupTests(HeroRegistryApiFactory factory)
    : IClassFixture<HeroRegistryApiFactory>
{
    // OpenAPI and Scalar behavior is covered in OpenApiAndScalarTests (T02.1).

    [Fact]
    public async Task Removed_template_endpoint_returns_not_found_to_a_signed_in_caller()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiClientExtensions.TokenFor(1, "Admin", isAdmin: true));

        using var response = await client.GetAsync("/weatherforecast", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_urls_reveal_nothing_to_anonymous_callers()
    {
        // The fallback policy (secure by default) also covers URLs that match no endpoint,
        // so an anonymous caller cannot probe which routes exist.
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/weatherforecast", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
