using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Persistence;

/// <summary>
/// Readiness check: opens a connection to SQL Server and runs <c>SELECT 1</c>.
/// </summary>
public sealed class SqlServerHealthCheck(IOptions<DatabaseOptions> options) : IHealthCheck
{
    public const string Name = "sqlserver";

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

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
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
