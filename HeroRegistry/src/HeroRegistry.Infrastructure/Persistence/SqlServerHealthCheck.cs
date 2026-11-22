using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry;

namespace HeroRegistry.Infrastructure.Persistence;

/// <summary>
/// Readiness check: opens a connection to SQL Server and runs <c>SELECT 1</c>.
/// </summary>
public sealed class SqlServerHealthCheck(IOptions<DatabaseOptions> options) : IHealthCheck
{
    public const string Name = "sqlserver";

    public const string Query = "SELECT 1";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var connectionString = options.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Connection string '{DatabaseOptions.ConnectionStringName}' is not configured.");
        }

        // Aspire polls /health every few seconds. Without this, each probe would send a SQL dependency
        // to Application Insights (cost and noise). The health request itself is already filtered out.
        using var suppressTelemetry = SuppressInstrumentationScope.Begin();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = Query;
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("SQL Server is reachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A health check must report failures, never throw them.
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "SQL Server is unreachable.",
                ex);
        }
    }
}
