using System.Net;
using HeroRegistry.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace HeroRegistry.Api.Tests.CrossCutting;

/// <summary>
/// Global exception handling: unexpected exceptions become a 500 problem without details, safety-net
/// exceptions get their proper status, and nothing sensitive reaches the response or the logs.
/// </summary>
public sealed class ExceptionHandlingTests : IClassFixture<HeroRegistryApiFactory>, IDisposable
{
    private readonly FakeLoggerProvider _logs = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ExceptionHandlingTests(HeroRegistryApiFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
            builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly));
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        _logs.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("bug")]
    [InlineData("database")]
    [InlineData("wrapped-database")]
    public async Task Unexpected_exceptions_return_a_500_problem_without_any_details(string kind)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/test-only/throw/{kind}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(Ct);
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("code").GetString().ShouldBe("server.error");
        problem.GetProperty("title").GetString().ShouldBe("An unexpected error occurred.");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        body.ShouldNotContain(ThrowingController.PersonalData);
        body.ShouldNotContain("Exception");
        body.ShouldNotContain(" at ");
    }

    [Fact]
    public async Task Code_bugs_are_logged_once_with_their_exception_for_diagnosis()
    {
        using var client = _factory.CreateClient();

        using (await client.GetAsync("/test-only/throw/bug", Ct)) { }

        var error = _logs.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeOfType<InvalidOperationException>();
        error.Message.ShouldContain("GET /test-only/throw/bug");
    }

    [Theory]
    [InlineData("database")]
    [InlineData("wrapped-database")]
    public async Task Database_errors_are_logged_without_their_message_which_can_echo_data(string kind)
    {
        using var client = _factory.CreateClient();

        using (await client.GetAsync($"/test-only/throw/{kind}", Ct)) { }

        var error = _logs.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeNull();
        error.Message.ShouldContain("FakeDbException");
        _logs.Collector.GetSnapshot().ShouldAllBe(r =>
            !r.Message.Contains(ThrowingController.PersonalData)
            && (r.Exception == null || !r.Exception.ToString().Contains(ThrowingController.PersonalData)));
    }

    [Fact]
    public async Task A_duplicate_that_escapes_the_use_cases_returns_409()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/test-only/throw/duplicate", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("code").GetString().ShouldBe("duplicate");
        problem.GetProperty("errors").TryGetProperty("heroName", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task A_domain_rule_that_escapes_the_use_cases_returns_400_on_its_field()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/test-only/throw/domain", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("code").GetString().ShouldBe("validation.failed");
        problem.GetProperty("errors").GetProperty("birthDate")[0].GetString().ShouldBe("BirthDate must be in the past.");
    }
}
