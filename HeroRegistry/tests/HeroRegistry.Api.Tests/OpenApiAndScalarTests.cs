using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// T02.1: the OpenAPI document and the Scalar API reference are available in Development only.
/// </summary>
public class OpenApiAndScalarTests(HeroRegistryApiFactory factory)
    : IClassFixture<HeroRegistryApiFactory>
{
    private const string OpenApiDocumentPath = "/openapi/v1.json";
    private const string ScalarPath = "/scalar";

    // Development (the WebApplicationFactory default)

    [Fact]
    public async Task Scalar_reference_is_served_in_Development()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ScalarPath, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task OpenApi_document_describes_the_Hero_Registry_API()
    {
        using var document = await GetOpenApiDocumentAsync();

        var info = document.RootElement.GetProperty("info");
        info.GetProperty("title").GetString().ShouldBe("Hero Registry API");
        info.GetProperty("version").GetString().ShouldBe("v1");
        info.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenApi_document_lists_the_API_endpoints()
    {
        using var document = await GetOpenApiDocumentAsync();

        PathsOf(document).ShouldContain("/api/v1/test");
    }

    [Fact]
    public async Task Root_redirects_to_Scalar_in_Development()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.ShouldBe(ScalarPath);
    }

    [Fact]
    public async Task Root_redirect_is_not_part_of_the_OpenAPI_document()
    {
        using var document = await GetOpenApiDocumentAsync();

        PathsOf(document).ShouldNotContain("/");
    }

    // Other environments: the API surface must not be published

    [Theory]
    [InlineData(OpenApiDocumentPath)]
    [InlineData(ScalarPath)]
    [InlineData("/")]
    public async Task Api_reference_is_not_exposed_in_Production(string path)
    {
        using var productionFactory = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Production));
        using var client = productionFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(OpenApiDocumentPath, CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var stream = await response.Content.ReadAsStreamAsync(CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: CancellationToken);
    }

    private static List<string> PathsOf(JsonDocument document) =>
        document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
}
