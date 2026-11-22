using System.Data.Common;
using HeroRegistry.Infrastructure.Persistence;
using HeroRegistry.Infrastructure.Persistence.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Migrations;

/// <summary>
/// The startup hook: migrations run before the API serves requests, with retries while SQL Server
/// is still starting (for example, a fresh Docker Compose stack).
/// </summary>
public class DatabaseInitializerTests
{
    private static readonly MigrationResult UpToDate = new([], TotalScripts: 2);

    private readonly IDatabaseMigrator _migrator = Substitute.For<IDatabaseMigrator>();
    private readonly FakeLogger<DatabaseInitializer> _logger = new();

    [Fact]
    public async Task Runs_the_migrations_on_startup()
    {
        _migrator.MigrateAsync(Arg.Any<CancellationToken>()).Returns(UpToDate);

        await Create().StartAsync(TestContext.Current.CancellationToken);

        await _migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retries_database_errors_until_SQL_Server_is_ready()
    {
        _migrator.MigrateAsync(Arg.Any<CancellationToken>()).Returns(
            _ => throw new TransientDbException(),
            _ => throw new TransientDbException(),
            _ => Task.FromResult(UpToDate));

        await Create(maxAttempts: 5).StartAsync(TestContext.Current.CancellationToken);

        await _migrator.Received(3).MigrateAsync(Arg.Any<CancellationToken>());
        _logger.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Warning).ShouldBe(2);
    }

    [Fact]
    public async Task Stops_the_application_after_the_last_attempt()
    {
        _migrator.MigrateAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new TransientDbException());

        await Should.ThrowAsync<TransientDbException>(
            () => Create(maxAttempts: 3).StartAsync(TestContext.Current.CancellationToken));

        await _migrator.Received(3).MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_retry_errors_that_are_not_database_errors()
    {
        // For example, an edited script that was already applied: retrying cannot fix it.
        _migrator.MigrateAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("edited"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => Create(maxAttempts: 5).StartAsync(TestContext.Current.CancellationToken));

        await _migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Skips_with_a_warning_when_no_connection_string_is_configured(string connectionString)
    {
        await Create(connectionString: connectionString).StartAsync(TestContext.Current.CancellationToken);

        await _migrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task Skips_when_migrations_on_startup_are_disabled()
    {
        await Create(migrateOnStartup: false).StartAsync(TestContext.Current.CancellationToken);

        await _migrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stopping_does_nothing()
    {
        await Create().StopAsync(TestContext.Current.CancellationToken);

        _migrator.ReceivedCalls().ShouldBeEmpty();
    }

    private DatabaseInitializer Create(
        string connectionString = "Server=sql;Database=HeroRegistryDb;",
        bool migrateOnStartup = true,
        int maxAttempts = 3) =>
        new(_migrator,
            Options.Create(new DatabaseOptions
            {
                ConnectionString = connectionString,
                MigrateOnStartup = migrateOnStartup,
                MigrationMaxAttempts = maxAttempts,
                MigrationRetryDelay = TimeSpan.Zero,
            }),
            _logger,
            TimeProvider.System);

    private sealed class TransientDbException() : DbException("SQL Server is starting up.");
}
