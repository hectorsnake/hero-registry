using System.Net;
using System.Net.Http.Headers;
using System.Text;
using HeroRegistry.Api.Tests.Fixtures;

namespace HeroRegistry.Api.Tests.CrossCutting;

/// <summary>
/// Every error response is RFC 9457 problem details with a stable <c>code</c> and a <c>traceId</c>
/// (the Application Insights operation id), including errors produced outside the use cases:
/// authentication, authorization, routing and model binding.
/// </summary>
public class ProblemDetailsTests(HeroRegistryApiFactory factory) : IClassFixture<HeroRegistryApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Authentication and authorization (previously empty bodies)

    [Fact]
    public async Task A_missing_token_returns_a_401_problem_and_the_Bearer_challenge()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/heroes", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer");
        await ShouldBeProblemAsync(response, "auth.unauthenticated");
    }

    [Fact]
    public async Task An_invalid_token_returns_a_401_problem_with_the_invalid_token_challenge()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        using var response = await client.GetAsync("/api/heroes", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("invalid_token");
        await ShouldBeProblemAsync(response, "auth.unauthenticated");
    }

    [Fact]
    public async Task A_regular_user_on_an_admin_only_endpoint_gets_a_403_problem()
    {
        using var client = ClientFor(ApiClientExtensions.TokenFor(7, "peter.parker"));

        using var response = await client.GetAsync("/api/users", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ShouldBeProblemAsync(response, "auth.forbidden");
    }

    [Fact]
    public async Task An_unknown_url_returns_a_404_problem_to_a_signed_in_caller()
    {
        using var client = ClientFor(ApiClientExtensions.TokenFor(1, "Admin", isAdmin: true));

        using var response = await client.GetAsync("/api/does-not-exist", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ShouldBeProblemAsync(response, expectedCode: null);
    }

    // Model binding (same shape as the use-case validation errors)

    [Fact]
    public async Task A_body_that_is_not_JSON_returns_a_validation_problem_without_parser_internals()
    {
        using var client = ClientFor(ApiClientExtensions.TokenFor(1, "Admin", isAdmin: true));
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/heroes", content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ShouldBeProblemAsync(response, "validation.failed");
        problem.GetProperty("errors").GetProperty("body")[0].GetString().ShouldBe("The request body is not valid JSON.");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("LineNumber");
    }

    [Fact]
    public async Task A_value_of_the_wrong_type_is_reported_on_its_camelCase_field_without_type_names()
    {
        using var client = ClientFor(ApiClientExtensions.TokenFor(1, "Admin", isAdmin: true));
        using var content = new StringContent("""{ "heroName": "Bad Date", "birthDate": "1990-13-45" }""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/heroes", content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ShouldBeProblemAsync(response, "validation.failed");
        problem.GetProperty("errors").GetProperty("birthDate")[0].GetString().ShouldBe("birthDate has an invalid value.");
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("System.");
        body.ShouldNotContain("\"$.");
    }

    [Fact]
    public async Task An_invalid_query_value_is_reported_on_its_camelCase_field()
    {
        using var client = ClientFor(ApiClientExtensions.TokenFor(1, "Admin", isAdmin: true));

        using var response = await client.GetAsync("/api/heroes?sortBy=Password", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ShouldBeProblemAsync(response, "validation.failed");
        problem.GetProperty("errors").TryGetProperty("sortBy", out _).ShouldBeTrue();
    }

    private HttpClient ClientFor(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<System.Text.Json.JsonElement> ShouldBeProblemAsync(HttpResponseMessage response, string? expectedCode)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("status").GetInt32().ShouldBe((int)response.StatusCode);
        problem.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        if (expectedCode is not null)
        {
            problem.GetProperty("code").GetString().ShouldBe(expectedCode);
        }

        return problem;
    }
}
