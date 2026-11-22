using System.Diagnostics;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Diagnostics;

/// <summary>
/// Integration tests against a real SQL Server (Testcontainers) for the SQL command tracing that feeds
/// Application Insights "dependencies". The instrumentation is configured exactly as in the API
/// (ServiceDefaults' AddHeroRegistrySqlClientInstrumentation).
/// </summary>
public sealed class SqlClientTelemetryTests(SqlServerContainerFixture sqlServer)
{
    private const string PersonalData = "someone@example.com";

    [Fact]
    public async Task Sql_commands_are_traced_with_their_text_but_without_parameter_values()
    {
        var marker = NewMarker();
        var spans = await TraceAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT @Email AS [{marker}]";
            command.Parameters.AddWithValue("@Email", PersonalData);
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        });

        var span = spans.Where(s => QueryText(s)?.Contains(marker) == true).ShouldHaveSingleItem();
        span.Kind.ShouldBe(ActivityKind.Client);
        QueryText(span)!.ShouldContain("@Email");
        AllRecordedText(span).ShouldNotContain(text => text.Contains(PersonalData));
    }

    [Fact]
    public async Task Failed_commands_are_errors_without_leaking_the_values_SQL_Server_echoes()
    {
        // A duplicate key violation: SQL Server's message contains the duplicate value.
        var marker = NewMarker();
        var spans = await TraceAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"CREATE TABLE #{marker} (Email NVARCHAR(100) PRIMARY KEY); " +
                $"INSERT INTO #{marker} VALUES (@Email); INSERT INTO #{marker} VALUES (@Email);";
            command.Parameters.AddWithValue("@Email", PersonalData);

            var exception = await Should.ThrowAsync<SqlException>(
                () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            exception.Message.ShouldContain(PersonalData); // proves the echo really happens
        });

        var span = spans.Where(s => QueryText(s)?.Contains(marker) == true).ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.Events.ShouldNotContain(e => e.Name == "exception");
        AllRecordedText(span).ShouldNotContain(text => text.Contains(PersonalData));
    }

    [Fact]
    public async Task Health_check_queries_are_not_traced()
    {
        var spans = await TraceAsync(async _ =>
        {
            var healthCheck = new SqlServerHealthCheck(
                Options.Create(new DatabaseOptions { ConnectionString = sqlServer.ConnectionString }));
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration(
                    SqlServerHealthCheck.Name, healthCheck, HealthStatus.Unhealthy, tags: null),
            };

            var result = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);
            result.Status.ShouldBe(HealthStatus.Healthy);
        });

        // The instrumentation replaces literals with '?', so "SELECT 1" would be recorded as "SELECT ?".
        // Other tests' spans cannot match: their queries contain unique markers.
        spans.ShouldNotContain(s => QueryText(s) == "SELECT ?");
    }

    [Fact]
    public async Task Literal_values_in_SQL_text_are_replaced_by_placeholders()
    {
        // A second safeguard in case a value were ever inlined into SQL instead of parameterized.
        var marker = NewMarker();
        var spans = await TraceAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT '{PersonalData}' AS [{marker}]";
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        });

        var span = spans.Where(s => QueryText(s)?.Contains(marker) == true).ShouldHaveSingleItem();
        AllRecordedText(span).ShouldNotContain(text => text.Contains(PersonalData));
    }

    private async Task<List<Activity>> TraceAsync(Func<SqlConnection, Task> work)
    {
        var exported = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHeroRegistrySqlClientInstrumentation()
            .AddInMemoryExporter(exported)
            .Build();

        await using (var connection = new SqlConnection(sqlServer.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await work(connection);
        }

        tracerProvider.ForceFlush();
        lock (exported)
        {
            return [.. exported];
        }
    }

    // The tag name depends on the semantic-convention version: db.query.text (stable) or db.statement (old).
    private static string? QueryText(Activity span) =>
        span.GetTagItem("db.query.text") as string ?? span.GetTagItem("db.statement") as string;

    private static IEnumerable<string> AllRecordedText(Activity span) =>
        span.TagObjects.Select(t => t.Value?.ToString() ?? string.Empty)
            .Concat(span.Events.SelectMany(e => e.Tags.Select(t => t.Value?.ToString() ?? string.Empty)))
            .Append(span.StatusDescription ?? string.Empty)
            .Append(span.DisplayName);

    private static string NewMarker() => $"m{Guid.NewGuid():N}";
}
