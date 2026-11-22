using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Api.Tests.CrossCutting;

/// <summary>
/// CORS: only the configured UI origins (Cors:AllowedOrigins) may call the API from a browser.
/// Defaults: http://localhost:4200 (Angular dev server) and http://localhost:8080 (Docker Compose UI).
/// </summary>
public class CorsTests(HeroRegistryApiFactory factory) : IClassFixture<HeroRegistryApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("http://localhost:8080")]
    public async Task A_preflight_from_an_allowed_origin_succeeds(string origin)
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight(origin, "PUT"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([origin]);
        string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant().ShouldContain("authorization");
        string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")).ShouldContain("PUT");
    }

    [Fact]
    public async Task A_preflight_from_an_unknown_origin_gets_no_CORS_permission()
    {
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("https://evil.example", "GET"), Ct);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Responses_to_an_allowed_origin_expose_the_Location_header()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test");
        request.Headers.Add("Origin", "http://localhost:4200");

        using var response = await client.SendAsync(request, Ct);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://localhost:4200"]);
        response.Headers.GetValues("Access-Control-Expose-Headers").ShouldContain(h => h.Contains("Location"));
    }

    [Fact]
    public async Task Allowed_origins_come_from_configuration()
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Cors:AllowedOrigins:0", "https://heroes.example.com");
            builder.UseSetting("Cors:AllowedOrigins:1", "");
        });
        using var client = configured.CreateClient();

        using var allowed = await client.SendAsync(Preflight("https://heroes.example.com", "GET"), Ct);
        using var noLongerAllowed = await client.SendAsync(Preflight("http://localhost:4200", "GET"), Ct);

        allowed.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["https://heroes.example.com"]);
        noLongerAllowed.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://localhost:4200/")]
    [InlineData("localhost:4200")]
    [InlineData("ftp://localhost")]
    [InlineData("http://localhost:4200/app")]
    public void An_invalid_origin_stops_the_API_at_startup(string origin)
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", origin));

        Should.Throw<OptionsValidationException>(() => configured.CreateClient()).Message.ShouldContain("Cors:AllowedOrigins");
    }

    private static HttpRequestMessage Preflight(string origin, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/heroes");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}
