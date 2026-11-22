using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeroRegistry.Infrastructure.Persistence.Migrations;

/// <summary>
/// Runs the database migrations when the API starts, before it serves requests. The same code runs
/// under Aspire (development) and Docker Compose (delivery), so no manual database step is needed.
/// Database errors are retried while SQL Server is still starting. Once the attempts are exhausted
/// the error stops the application, which is better than serving requests against a missing schema.
/// </summary>
public sealed class DatabaseInitializer(
    IDatabaseMigrator migrator,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseInitializer> logger,
    TimeProvider timeProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.MigrateOnStartup)
        {
            MigrationLog.SkippedDisabled(logger);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            // For example, API integration tests without a database. /health reports it as unhealthy.
            MigrationLog.SkippedNoConnectionString(logger, DatabaseOptions.ConnectionStringName);
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await migrator.MigrateAsync(cancellationToken);
                return;
            }
            catch (DbException ex) when (attempt < settings.MigrationMaxAttempts)
            {
                MigrationLog.RetryingAfterFailure(
                    logger,
                    attempt,
                    settings.MigrationMaxAttempts,
                    ex.GetType().FullName!,
                    (ex as SqlException)?.Number,
                    settings.MigrationRetryDelay.TotalSeconds);

                await Task.Delay(settings.MigrationRetryDelay, timeProvider, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
