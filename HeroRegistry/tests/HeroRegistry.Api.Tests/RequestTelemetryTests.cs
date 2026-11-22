using System.Diagnostics;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace HeroRegistry.Api.Tests;

/// <summary>
/// Requests to endpoints become request telemetry (Application Insights "requests"),
/// while health probes are filtered out to avoid cost and noise.
/// The same OpenTelemetry pipeline feeds the Aspire dashboard and Application Insights,
/// so an in-memory exporter observes exactly what they would receive.
/// </summary>
public sealed class RequestTelemetryTests : IClassFixture<HeroRegistryApiFactory>, IDisposable
{
    private readonly List<Activity> _exported = [];
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    public RequestTelemetryTests(HeroRegistryApiFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryExporter(_exported))));
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Requests_to_endpoints_are_traced()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/test", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var span = await WaitForSpanAsync(s => Path(s) == "/api/v1/test");
        span.ShouldNotBeNull("no request telemetry was exported for /api/v1/test");
        span.Kind.ShouldBe(ActivityKind.Server);
        span.GetTagItem("http.request.method").ShouldBe("GET");
        span.GetTagItem("http.response.status_code").ShouldBe(200);
    }

    [Fact]
    public async Task Requests_of_a_signed_in_user_are_tagged_with_the_user_id_only()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", Fixtures.ApiClientExtensions.TokenFor(7, "peter.parker"));

        using (await client.GetAsync("/api/v1/test", TestContext.Current.CancellationToken)) { }

        var span = await WaitForSpanAsync(s => Path(s) == "/api/v1/test" && s.GetTagItem("enduser.id") is not null);
        span.ShouldNotBeNull("the request span has no enduser.id tag");
        span.GetTagItem("enduser.id").ShouldBe("7");
        span.TagObjects.ShouldNotContain(tag => tag.Value is string && ((string)tag.Value).Contains("peter.parker"));
    }

    [Fact]
    public async Task Anonymous_requests_have_no_user_tag()
    {
        using var client = _factory.CreateClient();

        using (await client.GetAsync("/api/v1/test", TestContext.Current.CancellationToken)) { }

        var span = await WaitForSpanAsync(s => Path(s) == "/api/v1/test");
        span.ShouldNotBeNull();
        span.GetTagItem("enduser.id").ShouldBeNull();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Health_probes_are_not_traced(string path)
    {
        using var client = _factory.CreateClient();

        // A traced request after the probe proves the pipeline flushed past it.
        using (await client.GetAsync(path, TestContext.Current.CancellationToken)) { }
        using (await client.GetAsync("/api/v1/test", TestContext.Current.CancellationToken)) { }

        (await WaitForSpanAsync(s => Path(s) == "/api/v1/test")).ShouldNotBeNull();
        Snapshot().ShouldNotContain(s => Path(s) == path);
    }

    private static string? Path(Activity span) => span.GetTagItem("url.path") as string;

    private List<Activity> Snapshot()
    {
        lock (_exported)
        {
            return [.. _exported];
        }
    }

    // The server span ends just after the response is sent, so poll briefly.
    private async Task<Activity?> WaitForSpanAsync(Func<Activity, bool> predicate)
    {
        var tracerProvider = _factory.Services.GetRequiredService<TracerProvider>();

        for (var attempt = 0; attempt < 50; attempt++)
        {
            tracerProvider.ForceFlush();

            var span = Snapshot().FirstOrDefault(predicate);
            if (span is not null)
            {
                return span;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return null;
    }
}
