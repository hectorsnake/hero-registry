using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Tests.Persistence;

public class SqlServerHealthCheckTests(SqlServerContainerFixture sqlServer)
{
    [Fact]
    public async Task Reports_healthy_when_SQL_Server_is_reachable()
    {
        var result = await CheckAsync(sqlServer.ConnectionString);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reports_failure_status_when_connection_string_is_missing(string connectionString)
    {
        var result = await CheckAsync(connectionString);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain(DatabaseOptions.ConnectionStringName);
    }

    [Fact]
    public async Task Reports_failure_status_instead_of_throwing_when_connection_string_is_malformed()
    {
        var result = await CheckAsync("this is not a connection string");

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task Reports_failure_status_when_SQL_Server_is_unreachable()
    {
        // Port 1 on loopback: nothing listens there, so the connection is refused quickly.
        var result = await CheckAsync("Server=127.0.0.1,1;User Id=sa;Password=x;Connect Timeout=2;Encrypt=False");

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Uses_the_registration_failure_status()
    {
        var result = await CheckAsync(string.Empty, failureStatus: HealthStatus.Degraded);

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    private static Task<HealthCheckResult> CheckAsync(
        string connectionString,
        HealthStatus failureStatus = HealthStatus.Unhealthy)
    {
        var healthCheck = new SqlServerHealthCheck(
            Options.Create(new DatabaseOptions { ConnectionString = connectionString }));

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                SqlServerHealthCheck.Name, healthCheck, failureStatus, tags: null),
        };

        return healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);
    }
}
